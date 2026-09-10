using System.Threading.Tasks;

namespace PluginCoverShuffle.Domain.Providers
{
    /// <summary>
    /// A source of cover artwork. Implementations must return normalized
    /// results and must not modify Playnite games or plugin storage
    /// themselves; that is the import pipeline's job.
    /// </summary>
    public interface ICoverProvider
    {
        /// <summary>Which <see cref="CoverSource"/> this provider produces covers for.</summary>
        CoverSource Source { get; }

        /// <summary>
        /// Finds candidate covers. For network providers this returns
        /// preview metadata only (<see cref="CoverAsset.PreviewUrl"/>); no
        /// image is downloaded until <see cref="DownloadAsync"/> is called
        /// for a specific selection.
        /// </summary>
        Task<CoverSearchResult> SearchAsync(CoverSearchRequest request);

        /// <summary>
        /// Materializes a candidate into a local file ready for
        /// <see cref="Services.CoverImportService.Import"/>. For a local
        /// file this is a no-op passthrough; for a network provider this
        /// downloads (or reuses a cached copy of) the full image.
        /// </summary>
        Task<CoverDownloadResult> DownloadAsync(CoverAsset asset);
    }
}
