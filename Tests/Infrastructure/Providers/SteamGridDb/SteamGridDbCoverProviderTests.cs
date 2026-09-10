using System;
using System.Collections.Generic;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Providers.SteamGridDb;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Providers.SteamGridDb
{
    public class SteamGridDbCoverProviderTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly FakeSteamGridDbClient _client = new FakeSteamGridDbClient();
        private readonly SteamGridDbCache _cache;
        private readonly SteamGridDbCoverProvider _provider;

        public SteamGridDbCoverProviderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "SteamGridDbCoverProviderTests_" + Guid.NewGuid().ToString("N"));
            _cache = new SteamGridDbCache(_tempDirectory);
            _provider = new SteamGridDbCoverProvider(_client, _cache, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void Source_IsSteamGridDb()
        {
            Assert.Equal(CoverSource.SteamGridDb, _provider.Source);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WithBlankQuery_FailsWithoutCallingClient()
        {
            var result = await _provider.SearchAsync(new CoverSearchRequest { Query = "  " });

            Assert.False(result.Success);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WithASingleUnambiguousMatch_MapsGridsDirectly()
        {
            _client.SearchGamesResult = SteamGridDbResult<List<SteamGridDbGameMatch>>.Ok(new List<SteamGridDbGameMatch>
            {
                new SteamGridDbGameMatch { Id = 42, Name = "Cyberpunk 2077" }
            });
            _client.GetGridsResult = SteamGridDbResult<List<SteamGridDbGrid>>.Ok(new List<SteamGridDbGrid>
            {
                new SteamGridDbGrid { Id = 7, Url = "https://cdn/full.png", Thumb = "https://cdn/thumb.png" }
            });

            var result = await _provider.SearchAsync(new CoverSearchRequest { Query = "Cyberpunk 2077" });

            Assert.True(result.Success);
            Assert.False(result.RequiresGameSelection);
            Assert.Single(result.Assets);
            Assert.Equal("7", result.Assets[0].SourceId);
            Assert.Equal("https://cdn/thumb.png", result.Assets[0].PreviewUrl);
            Assert.Equal("https://cdn/full.png", result.Assets[0].FullImageUrl);
            Assert.Null(result.Assets[0].FilePath);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WithMultipleGameMatches_ReturnsGameMatchesInsteadOfGuessing()
        {
            _client.SearchGamesResult = SteamGridDbResult<List<SteamGridDbGameMatch>>.Ok(new List<SteamGridDbGameMatch>
            {
                new SteamGridDbGameMatch { Id = 1, Name = "Fallout" },
                new SteamGridDbGameMatch { Id = 2, Name = "Fallout 2" },
                new SteamGridDbGameMatch { Id = 3, Name = "Fallout 3" }
            });

            var result = await _provider.SearchAsync(new CoverSearchRequest { Query = "Fallout" });

            Assert.True(result.Success);
            Assert.True(result.RequiresGameSelection);
            Assert.Empty(result.Assets);
            Assert.Equal(3, result.GameMatches.Count);
            Assert.Equal("1", result.GameMatches[0].ProviderGameId);
            Assert.Equal("Fallout", result.GameMatches[0].Name);
            Assert.Equal(0, _client.GetGridsCallCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WithSelectedProviderGameId_FetchesGridsForThatGameDirectly()
        {
            _client.GetGridsResult = SteamGridDbResult<List<SteamGridDbGrid>>.Ok(new List<SteamGridDbGrid>
            {
                new SteamGridDbGrid { Id = 7, Url = "https://cdn/full.png", Thumb = "https://cdn/thumb.png" }
            });

            var result = await _provider.SearchAsync(new CoverSearchRequest { SelectedProviderGameId = "3", SelectedProviderGameName = "Fallout 3" });

            Assert.True(result.Success);
            Assert.False(result.RequiresGameSelection);
            Assert.Single(result.Assets);
            Assert.Equal(3, _client.LastGetGridsGameId);
            Assert.Equal(0, _client.SearchGamesCallCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WithNoGameMatches_Fails()
        {
            _client.SearchGamesResult = SteamGridDbResult<List<SteamGridDbGameMatch>>.Ok(new List<SteamGridDbGameMatch>());

            var result = await _provider.SearchAsync(new CoverSearchRequest { Query = "Nonexistent Game" });

            Assert.False(result.Success);
        }

        [Fact]
        public async System.Threading.Tasks.Task SearchAsync_WhenApiKeyInvalid_ReturnsUserFriendlyMessage()
        {
            _client.SearchGamesResult = SteamGridDbResult<List<SteamGridDbGameMatch>>.Failed(SteamGridDbErrorKind.InvalidApiKey, "raw error");

            var result = await _provider.SearchAsync(new CoverSearchRequest { Query = "Cyberpunk 2077" });

            Assert.False(result.Success);
            Assert.Contains("API key", result.ErrorMessage);
        }

        [Fact]
        public async System.Threading.Tasks.Task DownloadAsync_DownloadsAndCachesTheImage()
        {
            _client.DownloadImageResult = SteamGridDbResult<byte[]>.Ok(new byte[] { 9, 9, 9 });
            var asset = new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "7", FullImageUrl = "https://cdn/full.png" };

            var result = await _provider.DownloadAsync(asset);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.LocalFilePath));
            Assert.Equal(1, _client.DownloadImageCallCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task DownloadAsync_CalledTwiceForSameGrid_OnlyDownloadsOnce()
        {
            _client.DownloadImageResult = SteamGridDbResult<byte[]>.Ok(new byte[] { 9, 9, 9 });
            var asset = new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "7", FullImageUrl = "https://cdn/full.png" };

            await _provider.DownloadAsync(asset);
            var second = await _provider.DownloadAsync(asset);

            Assert.True(second.Success);
            Assert.Equal(1, _client.DownloadImageCallCount);
        }

        [Fact]
        public async System.Threading.Tasks.Task DownloadAsync_WithInvalidAsset_Fails()
        {
            var asset = new CoverAsset { Source = CoverSource.LocalFile, SourceId = "not-sgdb" };

            var result = await _provider.DownloadAsync(asset);

            Assert.False(result.Success);
        }
    }
}
