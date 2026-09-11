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
    public class PlayniteMetadataCoverViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly CoverImportService _importService;
        private readonly FakeCoverProvider _provider = new FakeCoverProvider { Source = CoverSource.PlayniteMetadata };
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly PlayniteMetadataCoverViewModel _viewModel;

        public PlayniteMetadataCoverViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "PlayniteMetadataCoverViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger(), new ImageNormalizationService());
            _viewModel = new PlayniteMetadataCoverViewModel(_gameId, _provider, _importService, _repository, new FakeCoverShuffleLogger());
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
        public async Task LoadAsync_PopulatesResultsFromProvider()
        {
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", PreviewUrl = "C:\\cover.png" },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", PreviewUrl = "C:\\bg.png" });

            await _viewModel.LoadAsync();

            Assert.Equal(2, _viewModel.Results.Count);
        }

        [Fact]
        public async Task LoadAsync_MarksAlreadyPooledArtworkAsAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath });

            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", PreviewUrl = filePath },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", PreviewUrl = filePath });

            await _viewModel.LoadAsync();

            Assert.True(_viewModel.Results.Single(r => r.Asset.SourceId == "cover").AlreadyAdded);
            Assert.False(_viewModel.Results.Single(r => r.Asset.SourceId == "background").AlreadyAdded);
        }

        [Fact]
        public async Task LoadAsync_WhenProviderFails_SetsStatusToErrorMessage()
        {
            _provider.SearchResult = CoverSearchResult.Failed("This game has no artwork in Playnite yet.");

            await _viewModel.LoadAsync();

            Assert.Equal("This game has no artwork in Playnite yet.", _viewModel.StatusMessage);
            Assert.Empty(_viewModel.Results);
        }

        [Fact]
        public async Task AddAsync_ImportsTheSelectedArtwork_AndMarksItAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath },
                alreadyAdded: false);

            await _viewModel.AddAsync(item);

            Assert.True(item.AlreadyAdded);
            Assert.Single(_repository.GetCovers(_gameId));
        }

        [Fact]
        public async Task AddAsync_WhenAlreadyAdded_DoesNothing()
        {
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover" },
                alreadyAdded: true);

            await _viewModel.AddAsync(item);

            Assert.Equal(0, _provider.DownloadCallCount);
        }
    }
}
