namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>
    /// Caches downloaded SteamGridDB images on disk so the same grid is
    /// never re-downloaded, and previously fetched covers keep working
    /// offline.
    /// </summary>
    internal interface ISteamGridDbCache
    {
        bool TryGetCachedFile(int gridId, out string filePath);

        /// <summary>Stores downloaded bytes and returns the local file path.</summary>
        string SaveToCache(int gridId, byte[] imageBytes, string sourceUrl);
    }
}
