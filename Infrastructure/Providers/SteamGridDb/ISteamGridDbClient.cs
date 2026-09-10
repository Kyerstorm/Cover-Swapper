using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>
    /// Raw SteamGridDB API v2 access. Kept separate from
    /// <see cref="SteamGridDbCoverProvider"/> so HTTP/JSON concerns can be
    /// faked in tests without any network access.
    /// </summary>
    internal interface ISteamGridDbClient
    {
        Task<SteamGridDbResult<List<SteamGridDbGameMatch>>> SearchGamesAsync(string query, CancellationToken cancellationToken);

        Task<SteamGridDbResult<List<SteamGridDbGrid>>> GetGridsForGameAsync(int gameId, CancellationToken cancellationToken);

        Task<SteamGridDbResult<byte[]>> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken);

        /// <summary>Performs a minimal authenticated call purely to check whether the configured API key is accepted.</summary>
        Task<SteamGridDbResult<bool>> ValidateApiKeyAsync(CancellationToken cancellationToken);
    }
}
