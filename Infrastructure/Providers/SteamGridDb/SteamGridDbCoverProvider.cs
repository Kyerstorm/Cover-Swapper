using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>
    /// Adapts the raw <see cref="ISteamGridDbClient"/>/<see cref="ISteamGridDbCache"/>
    /// into the provider-independent <see cref="ICoverProvider"/> contract.
    /// This is the only class outside <see cref="SteamGridDbClient"/> that
    /// knows SteamGridDB exists; the rest of the plugin only ever sees
    /// <see cref="ICoverProvider"/>.
    /// </summary>
    internal class SteamGridDbCoverProvider : ICoverProvider
    {
        private readonly ISteamGridDbClient _client;
        private readonly ISteamGridDbCache _cache;
        private readonly ICoverShuffleLogger _logger;

        public CoverSource Source => CoverSource.SteamGridDb;

        public SteamGridDbCoverProvider(ISteamGridDbClient client, ISteamGridDbCache cache, ICoverShuffleLogger logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CoverSearchResult> SearchAsync(CoverSearchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.IsNullOrWhiteSpace(request.SelectedProviderGameId))
            {
                if (!int.TryParse(request.SelectedProviderGameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectedGameId))
                {
                    return CoverSearchResult.Failed("Invalid SteamGridDB game reference.");
                }

                return await SearchGridsForGameAsync(selectedGameId, request.SelectedProviderGameName).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return CoverSearchResult.Failed("Enter a game name to search SteamGridDB.");
            }

            var gameSearch = await _client.SearchGamesAsync(request.Query, CancellationToken.None).ConfigureAwait(false);
            if (!gameSearch.Success)
            {
                return CoverSearchResult.Failed(DescribeError(gameSearch.ErrorKind, gameSearch.ErrorMessage));
            }

            if (gameSearch.Value == null || gameSearch.Value.Count == 0)
            {
                return CoverSearchResult.Failed($"No SteamGridDB matches found for \"{request.Query}\".");
            }

            // A single match is unambiguous; anything more (e.g. "Fallout"
            // matching Fallout, Fallout 2, Fallout 3, ...) must never be
            // silently resolved by picking the first result — let the
            // caller ask the user which game they meant.
            if (gameSearch.Value.Count > 1)
            {
                var matches = gameSearch.Value
                    .Select(game => new CoverGameMatch { ProviderGameId = game.Id.ToString(CultureInfo.InvariantCulture), Name = game.Name })
                    .ToList();
                return CoverSearchResult.NeedsGameSelection(matches);
            }

            var matchedGame = gameSearch.Value[0];
            return await SearchGridsForGameAsync(matchedGame.Id, matchedGame.Name).ConfigureAwait(false);
        }

        private async Task<CoverSearchResult> SearchGridsForGameAsync(int gameId, string gameName)
        {
            var gridsResult = await _client.GetGridsForGameAsync(gameId, CancellationToken.None).ConfigureAwait(false);
            if (!gridsResult.Success)
            {
                return CoverSearchResult.Failed(DescribeError(gridsResult.ErrorKind, gridsResult.ErrorMessage));
            }

            var assets = (gridsResult.Value ?? new System.Collections.Generic.List<SteamGridDbGrid>())
                .Select(grid => new CoverAsset
                {
                    Source = CoverSource.SteamGridDb,
                    SourceId = grid.Id.ToString(CultureInfo.InvariantCulture),
                    PreviewUrl = grid.Thumb,
                    FullImageUrl = grid.Url
                })
                .ToArray();

            if (assets.Length == 0)
            {
                var forGame = string.IsNullOrWhiteSpace(gameName) ? "the selected game" : $"\"{gameName}\"";
                return CoverSearchResult.Failed($"No cover art found on SteamGridDB for {forGame}.");
            }

            return CoverSearchResult.Succeeded(assets);
        }

        public async Task<CoverDownloadResult> DownloadAsync(CoverAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.Source != CoverSource.SteamGridDb
                || string.IsNullOrWhiteSpace(asset.SourceId)
                || !int.TryParse(asset.SourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gridId))
            {
                return CoverDownloadResult.Failed("Invalid SteamGridDB cover reference.");
            }

            if (_cache.TryGetCachedFile(gridId, out var cachedPath))
            {
                _logger.Debug($"Using cached SteamGridDB image for grid '{gridId}'.");
                return CoverDownloadResult.Succeeded(cachedPath);
            }

            var downloadResult = await _client.DownloadImageAsync(asset.FullImageUrl, CancellationToken.None).ConfigureAwait(false);
            if (!downloadResult.Success)
            {
                return CoverDownloadResult.Failed(DescribeError(downloadResult.ErrorKind, downloadResult.ErrorMessage));
            }

            var filePath = _cache.SaveToCache(gridId, downloadResult.Value, asset.FullImageUrl);
            return CoverDownloadResult.Succeeded(filePath);
        }

        private static string DescribeError(SteamGridDbErrorKind kind, string message)
        {
            switch (kind)
            {
                case SteamGridDbErrorKind.MissingApiKey:
                    return "SteamGridDB API key is not configured. Add one in Cover Shuffle settings.";
                case SteamGridDbErrorKind.InvalidApiKey:
                    return "SteamGridDB rejected the configured API key. Check it in Cover Shuffle settings.";
                case SteamGridDbErrorKind.NetworkError:
                    return "Could not reach SteamGridDB. Check your internet connection and try again.";
                default:
                    return string.IsNullOrWhiteSpace(message) ? "SteamGridDB request failed." : message;
            }
        }
    }
}
