using System;
using System.IO;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.Infrastructure.Providers
{
    /// <summary>
    /// Wraps a file path the user has already chosen (via a file-selection
    /// dialog) into a normalized <see cref="CoverAsset"/>. Performs no
    /// copying or validation beyond existence; that is the import
    /// pipeline's responsibility. The file is already local, so
    /// <see cref="DownloadAsync"/> is a no-op passthrough.
    /// </summary>
    public class LocalFileCoverProvider : ICoverProvider
    {
        public CoverSource Source => CoverSource.LocalFile;

        public Task<CoverSearchResult> SearchAsync(CoverSearchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.LocalFilePath))
            {
                return Task.FromResult(CoverSearchResult.Failed("No local file was selected."));
            }

            if (!File.Exists(request.LocalFilePath))
            {
                return Task.FromResult(CoverSearchResult.Failed($"File '{request.LocalFilePath}' could not be found."));
            }

            var result = CoverSearchResult.Succeeded(new CoverAsset
            {
                Source = CoverSource.LocalFile,
                SourceId = null,
                FilePath = request.LocalFilePath
            });
            return Task.FromResult(result);
        }

        public Task<CoverDownloadResult> DownloadAsync(CoverAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return Task.FromResult(CoverDownloadResult.Succeeded(asset.FilePath));
        }
    }
}
