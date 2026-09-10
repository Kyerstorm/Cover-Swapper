using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PluginCoverShuffle.Infrastructure.Providers.SteamGridDb;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>In-memory fake of the raw SteamGridDB client for testing <see cref="SteamGridDbCoverProvider"/> without HTTP.</summary>
    internal class FakeSteamGridDbClient : ISteamGridDbClient
    {
        public SteamGridDbResult<List<SteamGridDbGameMatch>> SearchGamesResult { get; set; }
            = SteamGridDbResult<List<SteamGridDbGameMatch>>.Ok(new List<SteamGridDbGameMatch>());

        public SteamGridDbResult<List<SteamGridDbGrid>> GetGridsResult { get; set; }
            = SteamGridDbResult<List<SteamGridDbGrid>>.Ok(new List<SteamGridDbGrid>());

        public SteamGridDbResult<byte[]> DownloadImageResult { get; set; }
            = SteamGridDbResult<byte[]>.Ok(new byte[0]);

        public SteamGridDbResult<bool> ValidateApiKeyResult { get; set; } = SteamGridDbResult<bool>.Ok(true);

        public int DownloadImageCallCount { get; private set; }

        public int SearchGamesCallCount { get; private set; }

        public int GetGridsCallCount { get; private set; }

        public int LastGetGridsGameId { get; private set; }

        public Task<SteamGridDbResult<List<SteamGridDbGameMatch>>> SearchGamesAsync(string query, CancellationToken cancellationToken)
        {
            SearchGamesCallCount++;
            return Task.FromResult(SearchGamesResult);
        }

        public Task<SteamGridDbResult<List<SteamGridDbGrid>>> GetGridsForGameAsync(int gameId, CancellationToken cancellationToken)
        {
            GetGridsCallCount++;
            LastGetGridsGameId = gameId;
            return Task.FromResult(GetGridsResult);
        }

        public Task<SteamGridDbResult<byte[]>> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
        {
            DownloadImageCallCount++;
            return Task.FromResult(DownloadImageResult);
        }

        public Task<SteamGridDbResult<bool>> ValidateApiKeyAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(ValidateApiKeyResult);
        }
    }
}
