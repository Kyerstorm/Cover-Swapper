using System;
using System.IO;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Providers;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Providers
{
    public class PlayniteMetadataCoverProviderTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly FakePlayniteArtworkSource _artworkSource = new FakePlayniteArtworkSource();
        private readonly PlayniteMetadataCoverProvider _provider;

        public PlayniteMetadataCoverProviderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "PlayniteMetadataCoverProviderTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _provider = new PlayniteMetadataCoverProvider(_artworkSource);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateFile(string name)
        {
            var path = Path.Combine(_tempDirectory, name);
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }

        [Fact]
        public void Source_IsPlayniteMetadata()
        {
            Assert.Equal(CoverSource.PlayniteMetadata, _provider.Source);
        }

        [Fact]
        public async Task SearchAsync_ForUnknownGame_Fails()
        {
            var result = await _provider.SearchAsync(new CoverSearchRequest { GameId = Guid.NewGuid() });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SearchAsync_WithNoArtworkSet_Fails()
        {
            var gameId = Guid.NewGuid();
            _artworkSource.Seed(gameId, new PlayniteGameArtwork());

            var result = await _provider.SearchAsync(new CoverSearchRequest { GameId = gameId });

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SearchAsync_ReturnsOneAssetPerAvailableArtworkSlot()
        {
            var gameId = Guid.NewGuid();
            _artworkSource.Seed(gameId, new PlayniteGameArtwork
            {
                CoverImagePath = CreateFile("cover.png"),
                BackgroundImagePath = CreateFile("background.png")
                // IconPath intentionally left null.
            });

            var result = await _provider.SearchAsync(new CoverSearchRequest { GameId = gameId });

            Assert.True(result.Success);
            Assert.Equal(2, result.Assets.Count);
            Assert.Contains(result.Assets, a => a.SourceId == "cover");
            Assert.Contains(result.Assets, a => a.SourceId == "background");
            Assert.DoesNotContain(result.Assets, a => a.SourceId == "icon");
            Assert.All(result.Assets, a => Assert.Equal(CoverSource.PlayniteMetadata, a.Source));
        }

        [Fact]
        public async Task DownloadAsync_ForExistingFile_SucceedsWithoutCopying()
        {
            var filePath = CreateFile("cover.png");
            var asset = new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath };

            var result = await _provider.DownloadAsync(asset);

            Assert.True(result.Success);
            Assert.Equal(filePath, result.LocalFilePath);
        }

        [Fact]
        public async Task DownloadAsync_WhenFileNoLongerExists_Fails()
        {
            var asset = new CoverAsset
            {
                Source = CoverSource.PlayniteMetadata,
                SourceId = "cover",
                FilePath = Path.Combine(_tempDirectory, "missing.png")
            };

            var result = await _provider.DownloadAsync(asset);

            Assert.False(result.Success);
        }
    }
}
