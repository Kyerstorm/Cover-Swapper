using System;
using System.Globalization;
using System.IO;

namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>
    /// File-backed cache under the plugin's own Cache directory. File names
    /// are built only from a numeric grid ID and a whitelisted extension —
    /// never from a raw URL or other external string — so no unsafe path
    /// construction is possible.
    /// </summary>
    internal class SteamGridDbCache : ISteamGridDbCache
    {
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

        private readonly string _cacheDirectory;

        public SteamGridDbCache(string cacheDirectory)
        {
            if (string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new ArgumentException("Cache directory must be provided.", nameof(cacheDirectory));
            }

            _cacheDirectory = cacheDirectory;
            Directory.CreateDirectory(_cacheDirectory);
        }

        public bool TryGetCachedFile(int gridId, out string filePath)
        {
            filePath = FindExistingFile(gridId);
            return filePath != null;
        }

        public string SaveToCache(int gridId, byte[] imageBytes, string sourceUrl)
        {
            var extension = GetExtensionFromUrl(sourceUrl);
            var filePath = Path.Combine(_cacheDirectory, FileNamePrefix(gridId) + extension);
            File.WriteAllBytes(filePath, imageBytes);
            return filePath;
        }

        private string FindExistingFile(int gridId)
        {
            var prefix = FileNamePrefix(gridId);
            foreach (var extension in AllowedExtensions)
            {
                var candidate = Path.Combine(_cacheDirectory, prefix + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string FileNamePrefix(int gridId) => "sgdb_" + gridId.ToString(CultureInfo.InvariantCulture);

        private static string GetExtensionFromUrl(string sourceUrl)
        {
            try
            {
                var extension = Path.GetExtension(new Uri(sourceUrl).AbsolutePath).ToLowerInvariant();
                return Array.IndexOf(AllowedExtensions, extension) >= 0 ? extension : ".png";
            }
            catch (Exception ex) when (ex is UriFormatException || ex is ArgumentException)
            {
                return ".png";
            }
        }
    }
}
