namespace PluginCoverShuffle.Services
{
    /// <summary>Outcome of <see cref="ImageNormalizationService.Normalize"/>.</summary>
    public class ImageNormalizationResult
    {
        public bool Success { get; }

        /// <summary>
        /// The file to use from here on. Equal to the original source path
        /// when no normalization was needed; otherwise a new temp file that
        /// the caller is responsible for deleting once it is done with it.
        /// </summary>
        public string NormalizedFilePath { get; }

        /// <summary>True if the file was re-encoded and/or resized.</summary>
        public bool WasConverted { get; }

        /// <summary>User-facing explanation; set whenever <see cref="Success"/> is false.</summary>
        public string ErrorMessage { get; }

        private ImageNormalizationResult(bool success, string normalizedFilePath, bool wasConverted, string errorMessage)
        {
            Success = success;
            NormalizedFilePath = normalizedFilePath;
            WasConverted = wasConverted;
            ErrorMessage = errorMessage;
        }

        public static ImageNormalizationResult Unchanged(string sourceFilePath) =>
            new ImageNormalizationResult(true, sourceFilePath, false, null);

        public static ImageNormalizationResult Converted(string normalizedFilePath) =>
            new ImageNormalizationResult(true, normalizedFilePath, true, null);

        public static ImageNormalizationResult Failed(string errorMessage) =>
            new ImageNormalizationResult(false, null, false, errorMessage);
    }
}
