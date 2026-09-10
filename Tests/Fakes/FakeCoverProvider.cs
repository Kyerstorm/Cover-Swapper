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

        public int DownloadCallCount { get; private set; }

        public Task<CoverSearchResult> SearchAsync(CoverSearchRequest request) => Task.FromResult(SearchResult);

        public Task<CoverDownloadResult> DownloadAsync(CoverAsset asset)
        {
            DownloadCallCount++;
            return Task.FromResult(DownloadResult);
        }
    }
}
