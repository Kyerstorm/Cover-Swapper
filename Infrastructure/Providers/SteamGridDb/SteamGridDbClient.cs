using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>
    /// Talks to the SteamGridDB API v2. All HTTP/JSON details live here;
    /// nothing outside this file should know the endpoint shapes. The API
    /// key is read fresh on every call (never cached beyond the call) and
    /// never appears in a log message or exception text.
    /// </summary>
    internal class SteamGridDbClient : ISteamGridDbClient
    {
        private const string BaseUrl = "https://www.steamgriddb.com/api/v2/";

        private readonly HttpClient _httpClient;
        private readonly Func<string> _apiKeyProvider;
        private readonly ICoverShuffleLogger _logger;

        /// <param name="httpClient">
        /// Shared client owned by the composition root; this class never
        /// disposes it. Its <c>BaseAddress</c> need not be set — requests
        /// here always use an absolute or client-relative URL.
        /// </param>
        /// <param name="apiKeyProvider">Reads the current API key from settings at call time, so a key changed mid-session is picked up immediately.</param>
        public SteamGridDbClient(HttpClient httpClient, Func<string> apiKeyProvider, ICoverShuffleLogger logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<SteamGridDbResult<List<SteamGridDbGameMatch>>> SearchGamesAsync(string query, CancellationToken cancellationToken)
        {
            var relativeUrl = BaseUrl + "search/autocomplete/" + Uri.EscapeDataString(query ?? string.Empty);
            return SendAndParseAsync<List<SteamGridDbGameMatch>>(relativeUrl, cancellationToken);
        }

        public Task<SteamGridDbResult<List<SteamGridDbGrid>>> GetGridsForGameAsync(int gameId, CancellationToken cancellationToken)
        {
            var relativeUrl = BaseUrl + "grids/game/" + gameId.ToString(CultureInfo.InvariantCulture);
            return SendAndParseAsync<List<SteamGridDbGrid>>(relativeUrl, cancellationToken);
        }

        public async Task<SteamGridDbResult<byte[]>> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return SteamGridDbResult<byte[]>.Failed(SteamGridDbErrorKind.UnexpectedResponse, "No image URL was provided.");
            }

            try
            {
                // Image CDN requests carry no Authorization header: the key
                // is only for api.../www.steamgriddb.com/api/v2 endpoints.
                using (var response = await _httpClient.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return SteamGridDbResult<byte[]>.Failed(
                            SteamGridDbErrorKind.NetworkError,
                            $"Downloading the cover image failed ({(int)response.StatusCode}).");
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return SteamGridDbResult<byte[]>.Ok(bytes);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Warning(ex, "SteamGridDB image download failed.");
                return SteamGridDbResult<byte[]>.Failed(SteamGridDbErrorKind.NetworkError, "Could not download the selected cover image.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.Warning(ex, "SteamGridDB image download timed out.");
                return SteamGridDbResult<byte[]>.Failed(SteamGridDbErrorKind.NetworkError, "Downloading the selected cover image timed out.");
            }
        }

        public async Task<SteamGridDbResult<bool>> ValidateApiKeyAsync(CancellationToken cancellationToken)
        {
            // SteamGridDB has no dedicated "whoami" endpoint; a minimal
            // authenticated search is enough to distinguish an accepted key
            // (any HTTP success, even zero results) from a rejected one (401).
            var probe = await SearchGamesAsync("a", cancellationToken).ConfigureAwait(false);
            if (probe.ErrorKind == SteamGridDbErrorKind.InvalidApiKey
                || probe.ErrorKind == SteamGridDbErrorKind.MissingApiKey
                || probe.ErrorKind == SteamGridDbErrorKind.NetworkError)
            {
                return SteamGridDbResult<bool>.Failed(probe.ErrorKind, probe.ErrorMessage);
            }

            return SteamGridDbResult<bool>.Ok(true);
        }

        private async Task<SteamGridDbResult<T>> SendAndParseAsync<T>(string absoluteUrl, CancellationToken cancellationToken)
        {
            var apiKey = _apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.MissingApiKey, "SteamGridDB API key is not configured.");
            }

            HttpResponseMessage response;
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Warning(ex, "SteamGridDB request failed.");
                return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.NetworkError, "Could not reach SteamGridDB.");
            }
            catch (TaskCanceledException ex)
            {
                _logger.Warning(ex, "SteamGridDB request timed out.");
                return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.NetworkError, "The SteamGridDB request timed out.");
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.InvalidApiKey, "SteamGridDB rejected the configured API key.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warning($"SteamGridDB request failed with status {(int)response.StatusCode}.");
                    return SteamGridDbResult<T>.Failed(
                        SteamGridDbErrorKind.UnexpectedResponse,
                        $"SteamGridDB request failed ({(int)response.StatusCode}).");
                }

                // A malformed or truncated response can legitimately carry no
                // content; treat that as empty rather than let a
                // NullReferenceException escape to the caller.
                var content = response.Content != null
                    ? await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                    : string.Empty;

                try
                {
                    var envelope = JsonConvert.DeserializeObject<SteamGridDbEnvelope<T>>(content);
                    if (envelope == null || !envelope.Success)
                    {
                        var message = envelope?.Errors != null && envelope.Errors.Count > 0
                            ? string.Join("; ", envelope.Errors)
                            : "SteamGridDB returned an unsuccessful response.";
                        return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.UnexpectedResponse, message);
                    }

                    return SteamGridDbResult<T>.Ok(envelope.Data);
                }
                catch (JsonException ex)
                {
                    _logger.Warning(ex, "Could not parse SteamGridDB response.");
                    return SteamGridDbResult<T>.Failed(SteamGridDbErrorKind.UnexpectedResponse, "Could not understand SteamGridDB's response.");
                }
            }
        }
    }
}
