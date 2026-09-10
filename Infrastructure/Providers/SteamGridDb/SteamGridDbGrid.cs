namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>Raw SteamGridDB grid (cover artwork) DTO; not exposed outside this assembly.</summary>
    internal class SteamGridDbGrid
    {
        public int Id { get; set; }

        /// <summary>Full-resolution image URL.</summary>
        public string Url { get; set; }

        /// <summary>Thumbnail URL, used for search-result previews.</summary>
        public string Thumb { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string Style { get; set; }
    }
}
