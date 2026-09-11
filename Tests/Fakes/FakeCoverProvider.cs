using System;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>Scriptable <see cref="ICoverProvider"/> fake for testing UI/orchestration code without a real provider.</summary>
    public class FakeCoverProvider : ICoverProvider
    {
        public CoverSource Source { get; set; } = CoverSource.SteamGridDb;

        public CoverSearchResult SearchResult { get; set; } = CoverSearchResult.Succeeded();

        public CoverDownloadResult DownloadResult { get; set; } = CoverDownloadResult.Failed("Not configured.");

        /// <summary>
        /// Optional per-asset override for tests that add several distinct
        /// results in one batch (e.g. "Add All") and need each download to
        /// resolve to its own file rather than the single shared
        /// <see cref="DownloadResult"/>. Falls back to <see cref="DownloadResult"/> when unset.
        /// </summary>
        public Func<CoverAsset, CoverDownloadResult> DownloadResultFactory { get; set; }

        public int DownloadCallCount { get; private set; }

        public CoverSearchRequest LastSearchRequest { get; private set; }

        public Task<CoverSearchResult> SearchAsync(CoverSearchRequest request)
        {
            LastSearchRequest = request;
            return Task.FromResult(SearchResult);
        }

        public Task<CoverDownloadResult> DownloadAsync(CoverAsset asset)
        {
            DownloadCallCount++;
            return Task.FromResult(DownloadResultFactory != null ? DownloadResultFactory(asset) : DownloadResult);
        }
    }
}
