namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Central policy for what local files are acceptable as covers, shared
    /// by <see cref="Infrastructure.Storage.CoverStorage"/> (authoritative
    /// enforcement) and the local-file import preview UI (early feedback
    /// before a file is actually copied/stored).
    /// </summary>
    public static class CoverImportPolicy
    {
        public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

        public const long MaxFileSizeBytes = 20L * 1024 * 1024;

        public const int MaxImageDimensionPixels = 3000;

        public const string AllowedExtensionsDisplay = "PNG • JPG • JPEG • WEBP • BMP • GIF";
    }
}
