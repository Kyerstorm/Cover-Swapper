using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Services
{
    /// <summary>Outcome of <see cref="CoverImportService.Import"/>.</summary>
    public class CoverImportResult
    {
        public CoverImportStatus Status { get; }

        /// <summary>User-facing explanation; set whenever <see cref="IsSuccess"/> is false.</summary>
        public string Message { get; }

        /// <summary>The stored cover record; set only when <see cref="IsSuccess"/> is true.</summary>
        public Cover Cover { get; }

        public bool IsSuccess => Status == CoverImportStatus.Success;

        private CoverImportResult(CoverImportStatus status, string message, Cover cover)
        {
            Status = status;
            Message = message;
            Cover = cover;
        }

        public static CoverImportResult Ok(Cover cover) =>
            new CoverImportResult(CoverImportStatus.Success, null, cover);

        public static CoverImportResult Failed(CoverImportStatus status, string message) =>
            new CoverImportResult(status, message, null);
    }
}
