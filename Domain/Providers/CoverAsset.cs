namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>
    /// A single normalized cover image returned by a provider, ready to be
    /// handed to the import pipeline. Providers must not modify Playnite
    /// games or write to plugin storage themselves.
    /// </summary>
    public class CoverAsset
    {
        /// <summary>Where this cover came from.</summary>
        public CoverSource Source { get; set; }

        /// <summary>
        /// Identifier of the cover within its source system (e.g. a SteamGridDB
        /// grid ID). Null for local files.
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// A local, readable file path containing the actual image bytes.
        /// Populated for local files immediately, and for network providers
        /// only after <see cref="ICoverProvider.DownloadAsync"/> has run; null
        /// beforehand (e.g. while only a search preview is available).
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>Small preview/thumbnail URL, for network providers that can show search results before downloading.</summary>
        public string PreviewUrl { get; set; }

        /// <summary>Full-resolution image URL to fetch when the user adds this cover.</summary>
        public string FullImageUrl { get; set; }
    }
}
