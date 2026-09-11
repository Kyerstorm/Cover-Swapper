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
        public static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        public const long MaxFileSizeBytes = 20L * 1024 * 1024;

        public const int MaxImageDimensionPixels = 3000;

        /// <summary>
        /// Hard ceiling on pixel dimensions checked before an image is fully
        /// decoded, independent of <see cref="MaxImageDimensionPixels"/> (the
        /// resize target). This bounds worst-case decode memory for a small
        /// compressed file with an enormous declared resolution, while still
        /// letting ordinary oversized images (up to this ceiling) through to
        /// be downscaled rather than rejected.
        /// </summary>
        public const int MaxDecodeDimensionPixels = 12000;

        public const string AllowedExtensionsDisplay = "PNG • JPG • JPEG • BMP • GIF";
    }
}
