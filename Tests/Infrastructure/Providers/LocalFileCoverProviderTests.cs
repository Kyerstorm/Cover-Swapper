using System;
using System.IO;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Providers;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Providers
{
    public class LocalFileCoverProviderTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly LocalFileCoverProvider _provider = new LocalFileCoverProvider();

        public LocalFileCoverProviderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LocalFileCoverProviderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void Source_IsLocalFile()
        {
            Assert.Equal(CoverSource.LocalFile, _provider.Source);
        }

        [Fact]
        public async Task SearchAsync_WithExistingFile_ReturnsSingleAsset()
        {
            var filePath = Path.Combine(_tempDirectory, "cover.png");
            File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });

            var result = await _provider.SearchAsync(new CoverSearchRequest { LocalFilePath = filePath });

            Assert.True(result.Success);
            Assert.Single(result.Assets);
            Assert.Equal(filePath, result.Assets[0].FilePath);
            Assert.Equal(CoverSource.LocalFile, result.Assets[0].Source);
        }

        [Fact]
        public async Task SearchAsync_WithMissingFile_Fails()
        {
            var result = await _provider.SearchAsync(new CoverSearchRequest { LocalFilePath = Path.Combine(_tempDirectory, "missing.png") });

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task SearchAsync_WithNoFileSelected_Fails()
        {
            var result = await _provider.SearchAsync(new CoverSearchRequest { LocalFilePath = null });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task DownloadAsync_ReturnsTheSameLocalFilePath_WithoutCopying()
        {
            var filePath = Path.Combine(_tempDirectory, "cover.png");
            File.WriteAllBytes(filePath, new byte[] { 1, 2, 3 });
            var searchResult = await _provider.SearchAsync(new CoverSearchRequest { LocalFilePath = filePath });

            var downloadResult = await _provider.DownloadAsync(searchResult.Assets[0]);

            Assert.True(downloadResult.Success);
            Assert.Equal(filePath, downloadResult.LocalFilePath);
        }
    }
}
