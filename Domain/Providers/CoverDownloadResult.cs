namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>Result of <see cref="ICoverProvider.DownloadAsync"/>.</summary>
    public class CoverDownloadResult
    {
        public bool Success { get; set; }

        /// <summary>User-facing explanation when <see cref="Success"/> is false.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>A local, readable file path holding the downloaded (or already-local) image.</summary>
        public string LocalFilePath { get; set; }

        public static CoverDownloadResult Succeeded(string localFilePath)
        {
            return new CoverDownloadResult { Success = true, LocalFilePath = localFilePath };
        }

        public static CoverDownloadResult Failed(string errorMessage)
        {
            return new CoverDownloadResult { Success = false, ErrorMessage = errorMessage };
        }
    }
}
