namespace PluginCoverShuffle.Services
{
    /// <summary>Outcome of an <see cref="ImportExportService"/> operation.</summary>
    public class ImportExportResult
    {
        public bool Success { get; }

        public string Message { get; }

        private ImportExportResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ImportExportResult Ok(string message) => new ImportExportResult(true, message);

        public static ImportExportResult Failed(string message) => new ImportExportResult(false, message);
    }
}
