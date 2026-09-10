using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class SteamGridDbSearchViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly CoverImportService _importService;
        private readonly FakeCoverProvider _provider = new FakeCoverProvider();
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly SteamGridDbSearchViewModel _viewModel;

        public SteamGridDbSearchViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "SteamGridDbSearchViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger());
            _viewModel = new SteamGridDbSearchViewModel(_gameId, _provider, _importService, _repository, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateValidImageFile()
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        [Fact]
        public async Task SearchAsync_WithBlankQuery_SetsStatusAndDoesNotCallProvider()
        {
            _viewModel.SearchQuery = "";

            await _viewModel.SearchAsync();

            Assert.Empty(_viewModel.Results);
            Assert.False(string.IsNullOrEmpty(_viewModel.StatusMessage));
        }

        [Fact]
        public async Task SearchAsync_PopulatesResultsFromProvider()
        {
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" },
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "2", PreviewUrl = "https://cdn/2.png" });
            _viewModel.SearchQuery = "Cyberpunk 2077";

            await _viewModel.SearchAsync();

            Assert.Equal(2, _viewModel.Results.Count);
        }

        [Fact]
        public async Task SearchAsync_MarksResultsAlreadyInThePoolAsAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", FilePath = filePath });

            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" },
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "2", PreviewUrl = "https://cdn/2.png" });
            _viewModel.SearchQuery = "Cyberpunk 2077";

            await _viewModel.SearchAsync();

            Assert.True(_viewModel.Results.Single(r => r.Asset.SourceId == "1").AlreadyAdded);
            Assert.False(_viewModel.Results.Single(r => r.Asset.SourceId == "2").AlreadyAdded);
        }

        [Fact]
        public async Task SearchAsync_WhenProviderFails_SetsStatusToErrorMessage()
        {
            _provider.SearchResult = CoverSearchResult.Failed("SteamGridDB API key is not configured.");
            _viewModel.SearchQuery = "Cyberpunk 2077";

            await _viewModel.SearchAsync();

            Assert.Equal("SteamGridDB API key is not configured.", _viewModel.StatusMessage);
            Assert.Empty(_viewModel.Results);
        }

        [Fact]
        public async Task AddAsync_DownloadsAndImportsTheCover_AndMarksItAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", FullImageUrl = "https://cdn/1.png" },
                alreadyAdded: false);

            await _viewModel.AddAsync(item);

            Assert.True(item.AlreadyAdded);
            Assert.Single(_repository.GetCovers(_gameId));
        }

        [Fact]
        public async Task AddAsync_WhenAlreadyAdded_DoesNothing()
        {
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1" },
                alreadyAdded: true);

            await _viewModel.AddAsync(item);

            Assert.Equal(0, _provider.DownloadCallCount);
        }

        [Fact]
        public async Task AddAsync_WhenDownloadFails_ShowsErrorAndDoesNotImport()
        {
            _provider.DownloadResult = CoverDownloadResult.Failed("Could not reach SteamGridDB.");
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1" },
                alreadyAdded: false);

            await _viewModel.AddAsync(item);

            Assert.Equal("Could not reach SteamGridDB.", _viewModel.StatusMessage);
            Assert.False(item.AlreadyAdded);
            Assert.Empty(_repository.GetCovers(_gameId));
        }
    }
}
