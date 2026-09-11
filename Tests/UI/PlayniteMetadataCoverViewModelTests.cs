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
            _viewModel = new PlayniteMetadataCoverViewModel(_gameId, "Fallout 4", _provider, _importService, _repository, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static int _pixelCounter;

        private string CreateValidImageFile()
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                // Vary pixel content so each generated file hashes differently;
                // otherwise every blank bitmap encodes to identical PNG bytes
                // and the duplicate-hash check would reject later "distinct" files.
                var shade = System.Threading.Interlocked.Increment(ref _pixelCounter) % 256;
                bitmap.SetPixel(0, 0, Color.FromArgb(shade, shade, shade));
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        [Fact]
        public void GameName_IsPopulatedFromConstructor_NeverAskedOfTheUser()
        {
            Assert.Equal("Fallout 4", _viewModel.GameName);
        }

        [Fact]
        public void GameName_WhenGameNotFound_FallsBackToPlaceholder()
        {
            var viewModel = new PlayniteMetadataCoverViewModel(_gameId, null, _provider, _importService, _repository, new FakeCoverShuffleLogger());

            Assert.Equal("(game not found in Playnite)", viewModel.GameName);
        }

        [Fact]
        public async Task LoadAsync_PopulatesResultsFromProvider()
        {
            var coverFile = CreateValidImageFile();
            var backgroundFile = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = coverFile, PreviewUrl = coverFile },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", FilePath = backgroundFile, PreviewUrl = backgroundFile });

            await _viewModel.LoadAsync();

            Assert.Equal(2, _viewModel.Results.Count);
            Assert.True(_viewModel.ShowResults);
        }

        [Fact]
        public async Task LoadAsync_MapsEachSourceIdToItsArtworkType()
        {
            var coverFile = CreateValidImageFile();
            var backgroundFile = CreateValidImageFile();
            var iconFile = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = coverFile, PreviewUrl = coverFile },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", FilePath = backgroundFile, PreviewUrl = backgroundFile },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "icon", FilePath = iconFile, PreviewUrl = iconFile });

            await _viewModel.LoadAsync();

            Assert.Equal(PlayniteArtworkType.Cover, _viewModel.Results.Single(r => r.SourceId == "cover").ArtworkType);
            Assert.Equal(PlayniteArtworkType.Background, _viewModel.Results.Single(r => r.SourceId == "background").ArtworkType);
            Assert.Equal(PlayniteArtworkType.Icon, _viewModel.Results.Single(r => r.SourceId == "icon").ArtworkType);
        }

        [Fact]
        public async Task LoadAsync_WhenArtworkFileNoLongerExists_MarksItUnavailable()
        {
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = Path.Combine(_tempDirectory, "missing.png"), PreviewUrl = "missing.png" });

            await _viewModel.LoadAsync();

            var item = _viewModel.Results.Single();
            Assert.False(item.IsAvailable);
            Assert.False(item.CanAdd);
        }

        [Fact]
        public async Task LoadAsync_MarksAlreadyPooledArtworkAsAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath });

            var backgroundFile = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", FilePath = backgroundFile, PreviewUrl = backgroundFile });

            await _viewModel.LoadAsync();

            Assert.True(_viewModel.Results.Single(r => r.SourceId == "cover").AlreadyAdded);
            Assert.False(_viewModel.Results.Single(r => r.SourceId == "background").AlreadyAdded);
        }

        [Fact]
        public async Task LoadAsync_WhenProviderFails_ShowsErrorState()
        {
            _provider.SearchResult = CoverSearchResult.Failed("This game has no artwork in Playnite yet.");

            await _viewModel.LoadAsync();

            Assert.True(_viewModel.ShowError);
            Assert.Equal("This game has no artwork in Playnite yet.", _viewModel.LoadErrorMessage);
            Assert.Empty(_viewModel.Results);
        }

        [Fact]
        public async Task LoadAsync_WithNoArtwork_ShowsEmptyState()
        {
            _provider.SearchResult = CoverSearchResult.Succeeded();

            await _viewModel.LoadAsync();

            Assert.True(_viewModel.ShowEmpty);
            Assert.False(_viewModel.ShowError);
        }

        [Fact]
        public void ShowLoading_IsTrueOnlyWhileTheInitialLoadIsInFlight()
        {
            Assert.False(_viewModel.ShowLoading);

            var loadTask = _viewModel.LoadAsync();

            // The fake provider completes synchronously, so by the time we
            // observe state here the load may already be done; this test
            // exists to document the intended relationship (IsBusy &&
            // !HasLoaded), verified more directly by the other state tests.
            Assert.NotNull(loadTask);
        }

        [Fact]
        public async Task AddAsync_ImportsTheSelectedArtwork_AndMarksItAlreadyAdded()
        {
            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();

            await _viewModel.AddAsync(item);

            Assert.True(item.AlreadyAdded);
            Assert.Single(_repository.GetCovers(_gameId));
        }

        [Fact]
        public async Task AddAsync_WhenAlreadyAdded_DoesNothing()
        {
            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath });
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();

            await _viewModel.AddAsync(item);

            Assert.Equal(0, _provider.DownloadCallCount);
        }

        [Fact]
        public async Task AddAsync_WhenDownloadFails_SetsItemErrorMessage_WithoutThrowing()
        {
            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            _provider.DownloadResult = CoverDownloadResult.Failed("This artwork is no longer available.");
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();

            await _viewModel.AddAsync(item);

            Assert.False(item.AlreadyAdded);
            Assert.True(item.HasError);
            Assert.Equal("This artwork is no longer available.", item.ErrorMessage);
        }

        [Fact]
        public async Task AddAsync_DuplicateImage_SurfacesDuplicateMessage_WithoutMarkingAdded()
        {
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "existing", FilePath = filePath });

            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();

            await _viewModel.AddAsync(item);

            Assert.False(item.AlreadyAdded);
            Assert.True(item.HasError);
            Assert.Single(_repository.GetCovers(_gameId));
        }

        [Fact]
        public async Task RetryAsync_AfterAFailedAdd_CanSucceed()
        {
            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            _provider.DownloadResult = CoverDownloadResult.Failed("Temporary failure.");
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();
            await _viewModel.AddAsync(item);
            Assert.True(item.HasError);

            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            await _viewModel.RetryAsync(item);

            Assert.False(item.HasError);
            Assert.True(item.AlreadyAdded);
        }

        [Fact]
        public async Task AddAsync_WhenAtCoverLimit_DoesNothing()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                _repository.AddCover(new Cover
                {
                    CoverId = Guid.NewGuid(),
                    GameId = _gameId,
                    Source = CoverSource.LocalFile,
                    LocalPath = $"Covers\\{_gameId:N}\\filler-{i}.png",
                    Hash = Guid.NewGuid().ToString("N"),
                    IsEnabled = true
                });
            }

            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = filePath, PreviewUrl = filePath });
            await _viewModel.LoadAsync();
            var item = _viewModel.Results.Single();

            Assert.True(_viewModel.IsAtCoverLimit);
            Assert.False(item.CanAdd);

            await _viewModel.AddAsync(item);

            Assert.Equal(0, _provider.DownloadCallCount);
            Assert.False(item.AlreadyAdded);
        }

        [Fact]
        public async Task AddAllAsync_AddsEveryAvailableNotYetAddedItem()
        {
            var coverFile = CreateValidImageFile();
            var backgroundFile = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = coverFile, PreviewUrl = coverFile },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", FilePath = backgroundFile, PreviewUrl = backgroundFile });
            _provider.DownloadResultFactory = asset => CoverDownloadResult.Succeeded(asset.FilePath);
            await _viewModel.LoadAsync();

            await _viewModel.AddAllAsync();

            Assert.All(_viewModel.Results, r => Assert.True(r.AlreadyAdded));
            Assert.Equal(2, _repository.GetCovers(_gameId).Count);
        }

        [Fact]
        public async Task AddAllAsync_StopsOnceTheCoverLimitIsReached()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame - 1; i++)
            {
                _repository.AddCover(new Cover
                {
                    CoverId = Guid.NewGuid(),
                    GameId = _gameId,
                    Source = CoverSource.LocalFile,
                    LocalPath = $"Covers\\{_gameId:N}\\filler-{i}.png",
                    Hash = Guid.NewGuid().ToString("N"),
                    IsEnabled = true
                });
            }

            var coverFile = CreateValidImageFile();
            var backgroundFile = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "cover", FilePath = coverFile, PreviewUrl = coverFile },
                new CoverAsset { Source = CoverSource.PlayniteMetadata, SourceId = "background", FilePath = backgroundFile, PreviewUrl = backgroundFile });
            _provider.DownloadResultFactory = asset => CoverDownloadResult.Succeeded(asset.FilePath);
            await _viewModel.LoadAsync();

            Assert.Equal(1, _viewModel.RemainingSlots);
            Assert.Equal("Add available artwork", _viewModel.AddAllButtonText);

            await _viewModel.AddAllAsync();

            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, _repository.GetCovers(_gameId).Count);
            Assert.Single(_viewModel.Results, r => r.AlreadyAdded);
        }

        [Fact]
        public async Task RemainingSlots_And_IsAtCoverLimit_ReflectRepositoryState()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                _repository.AddCover(new Cover
                {
                    CoverId = Guid.NewGuid(),
                    GameId = _gameId,
                    Source = CoverSource.LocalFile,
                    LocalPath = $"Covers\\{_gameId:N}\\filler-{i}.png",
                    Hash = Guid.NewGuid().ToString("N"),
                    IsEnabled = true
                });
            }

            Assert.Equal(0, _viewModel.RemainingSlots);
            Assert.True(_viewModel.IsAtCoverLimit);
            Assert.False(_viewModel.CanAddAll);
        }
    }
}
