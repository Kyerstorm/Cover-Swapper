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

        public SteamGridDbSearchViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "SteamGridDbSearchViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private SteamGridDbSearchViewModel CreateViewModel(string gameName = null)
        {
            return new SteamGridDbSearchViewModel(_gameId, gameName, _provider, _importService, _repository, new FakeCoverShuffleLogger());
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

        private void FillCoverPool(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _repository.AddCover(new Cover
                {
                    CoverId = Guid.NewGuid(),
                    GameId = _gameId,
                    Source = CoverSource.LocalFile,
                    Hash = "hash-" + i,
                    LocalPath = "cover-" + i + ".png",
                    AddedAt = DateTime.UtcNow,
                    IsEnabled = true
                });
            }
        }

        [Fact]
        public void Constructor_WithGameName_AutoPopulatesSearchQuery()
        {
            var viewModel = CreateViewModel("Cyberpunk 2077");

            Assert.Equal("Cyberpunk 2077", viewModel.SearchQuery);
            Assert.Equal("Cyberpunk 2077", viewModel.GameName);
        }

        [Fact]
        public void Constructor_WithoutGameName_LeavesSearchQueryEmpty()
        {
            var viewModel = CreateViewModel(null);

            Assert.Null(viewModel.SearchQuery);
        }

        [Fact]
        public async Task SearchAsync_WithBlankQuery_SetsStatusAndDoesNotCallProvider()
        {
            var viewModel = CreateViewModel();
            viewModel.SearchQuery = "";

            await viewModel.SearchAsync();

            Assert.Empty(viewModel.Results);
            Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
        }

        [Fact]
        public async Task SearchAsync_PopulatesResultsFromProvider_AndShowsResultCount()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" },
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "2", PreviewUrl = "https://cdn/2.png" });
            viewModel.SearchQuery = "Cyberpunk 2077";

            await viewModel.SearchAsync();

            Assert.Equal(2, viewModel.Results.Count);
            Assert.Equal("2 covers found", viewModel.ResultCountText);
            Assert.True(viewModel.ShowResults);
            Assert.False(viewModel.ShowEmpty);
            Assert.False(viewModel.ShowError);
        }

        [Fact]
        public async Task SearchAsync_WithNoResults_ShowsEmptyState()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.Succeeded();
            viewModel.SearchQuery = "Some Obscure Game";

            await viewModel.SearchAsync();

            Assert.Empty(viewModel.Results);
            Assert.True(viewModel.ShowEmpty);
            Assert.False(viewModel.ShowResults);
            Assert.False(viewModel.ShowError);
        }

        [Fact]
        public async Task SearchAsync_MarksResultsAlreadyInThePoolAsAlreadyAdded()
        {
            var viewModel = CreateViewModel();
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", FilePath = filePath });

            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" },
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "2", PreviewUrl = "https://cdn/2.png" });
            viewModel.SearchQuery = "Cyberpunk 2077";

            await viewModel.SearchAsync();

            Assert.True(viewModel.Results.Single(r => r.Asset.SourceId == "1").AlreadyAdded);
            Assert.False(viewModel.Results.Single(r => r.Asset.SourceId == "2").AlreadyAdded);
        }

        [Fact]
        public async Task SearchAsync_WhenProviderReturnsMultipleGameMatches_ShowsGameMatchesInsteadOfCovers()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.NeedsGameSelection(new[]
            {
                new CoverGameMatch { ProviderGameId = "1", Name = "Fallout" },
                new CoverGameMatch { ProviderGameId = "3", Name = "Fallout 3" }
            });
            viewModel.SearchQuery = "Fallout";

            await viewModel.SearchAsync();

            Assert.True(viewModel.ShowGameMatches);
            Assert.False(viewModel.ShowResults);
            Assert.False(viewModel.ShowEmpty);
            Assert.Equal(2, viewModel.GameMatches.Count);
            Assert.Empty(viewModel.Results);
        }

        [Fact]
        public async Task SelectGameMatchAsync_FetchesCoversForThatGame_AndClearsGameMatches()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.NeedsGameSelection(new[]
            {
                new CoverGameMatch { ProviderGameId = "1", Name = "Fallout" },
                new CoverGameMatch { ProviderGameId = "3", Name = "Fallout 3" }
            });
            viewModel.SearchQuery = "Fallout";
            await viewModel.SearchAsync();
            var chosen = viewModel.GameMatches.Single(m => m.ProviderGameId == "3");

            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "99", PreviewUrl = "https://cdn/99.png" });

            await viewModel.SelectGameMatchAsync(chosen);

            Assert.False(viewModel.ShowGameMatches);
            Assert.True(viewModel.ShowResults);
            Assert.Empty(viewModel.GameMatches);
            Assert.Single(viewModel.Results);
            Assert.Equal("3", _provider.LastSearchRequest.SelectedProviderGameId);
            Assert.Equal("Fallout 3", _provider.LastSearchRequest.SelectedProviderGameName);
        }

        [Fact]
        public async Task SearchAsync_WhenProviderFails_ShowsErrorState()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.Failed("SteamGridDB API key is not configured.");
            viewModel.SearchQuery = "Cyberpunk 2077";

            await viewModel.SearchAsync();

            Assert.Equal("SteamGridDB API key is not configured.", viewModel.ErrorMessage);
            Assert.True(viewModel.ShowError);
            Assert.False(viewModel.ShowEmpty);
            Assert.False(viewModel.ShowResults);
            Assert.Empty(viewModel.Results);
        }

        [Fact]
        public void SelectedItem_DefaultsToNull_AndAddIsDisabled()
        {
            var viewModel = CreateViewModel();

            Assert.Null(viewModel.SelectedItem);
            Assert.False(viewModel.CanAddSelected);
            Assert.Equal("Selected: 0", viewModel.SelectedCountText);
        }

        [Fact]
        public async Task SelectingAResult_EnablesAddSelected()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" });
            viewModel.SearchQuery = "Cyberpunk 2077";
            await viewModel.SearchAsync();

            viewModel.SelectedItem = viewModel.Results.Single();

            Assert.True(viewModel.CanAddSelected);
            Assert.Equal("Selected: 1", viewModel.SelectedCountText);
        }

        [Fact]
        public async Task ChangingSelection_UpdatesSelectedItem()
        {
            var viewModel = CreateViewModel();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" },
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "2", PreviewUrl = "https://cdn/2.png" });
            viewModel.SearchQuery = "Cyberpunk 2077";
            await viewModel.SearchAsync();

            var first = viewModel.Results.First(r => r.Asset.SourceId == "1");
            var second = viewModel.Results.First(r => r.Asset.SourceId == "2");
            viewModel.SelectedItem = first;
            viewModel.SelectedItem = second;

            Assert.Same(second, viewModel.SelectedItem);
            Assert.True(viewModel.CanAddSelected);
        }

        [Fact]
        public async Task SelectingAnAlreadyAddedResult_DoesNotEnableAddSelected()
        {
            var viewModel = CreateViewModel();
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", FilePath = filePath });
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", PreviewUrl = "https://cdn/1.png" });
            viewModel.SearchQuery = "Cyberpunk 2077";
            await viewModel.SearchAsync();

            viewModel.SelectedItem = viewModel.Results.Single();

            Assert.True(viewModel.SelectedItem.AlreadyAdded);
            Assert.False(viewModel.CanAddSelected);
        }

        [Fact]
        public async Task AddSelectedAsync_DownloadsAndImportsTheSelectedCover_AndMarksItAlreadyAdded()
        {
            var viewModel = CreateViewModel();
            var filePath = CreateValidImageFile();
            _provider.SearchResult = CoverSearchResult.Succeeded(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1", FullImageUrl = "https://cdn/1.png" });
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            viewModel.SearchQuery = "Cyberpunk 2077";
            await viewModel.SearchAsync();
            viewModel.SelectedItem = viewModel.Results.Single();

            await viewModel.AddSelectedAsync();

            Assert.True(viewModel.SelectedItem.AlreadyAdded);
            Assert.False(viewModel.CanAddSelected);
            Assert.Single(_repository.GetCovers(_gameId));
        }

        [Fact]
        public async Task AddAsync_WhenAlreadyAdded_DoesNothing()
        {
            var viewModel = CreateViewModel();
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1" },
                alreadyAdded: true);

            await viewModel.AddAsync(item);

            Assert.Equal(0, _provider.DownloadCallCount);
        }

        [Fact]
        public async Task AddAsync_WhenDownloadFails_ShowsErrorAndDoesNotImport()
        {
            var viewModel = CreateViewModel();
            _provider.DownloadResult = CoverDownloadResult.Failed("Could not reach SteamGridDB.");
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "1" },
                alreadyAdded: false);

            await viewModel.AddAsync(item);

            Assert.Equal("Could not reach SteamGridDB.", viewModel.ErrorMessage);
            Assert.False(item.AlreadyAdded);
            Assert.Empty(_repository.GetCovers(_gameId));
        }

        [Fact]
        public void WhenGameAlreadyHasMaxCovers_IsAtCoverLimitIsTrue_AndAddSelectedIsDisabled()
        {
            FillCoverPool(CoverLimitPolicy.MaxCoversPerGame);
            var viewModel = CreateViewModel();

            Assert.True(viewModel.IsAtCoverLimit);

            viewModel.SelectedItem = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "not-yet-added" },
                alreadyAdded: false);

            Assert.False(viewModel.CanAddSelected);
        }

        [Fact]
        public async Task AddAsync_WhenGameAlreadyHasMaxCovers_DoesNotImport()
        {
            FillCoverPool(CoverLimitPolicy.MaxCoversPerGame);
            var viewModel = CreateViewModel();
            var filePath = CreateValidImageFile();
            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "one-too-many" },
                alreadyAdded: false);

            await viewModel.AddAsync(item);

            Assert.False(item.AlreadyAdded);
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, _repository.GetCovers(_gameId).Count);
        }

        [Fact]
        public async Task AddAsync_WhenImportRejectsAsDuplicateHash_ShowsErrorAndDoesNotAddASecondCover()
        {
            var viewModel = CreateViewModel();
            var filePath = CreateValidImageFile();
            _importService.Import(_gameId, new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "existing", FilePath = filePath });

            _provider.DownloadResult = CoverDownloadResult.Succeeded(filePath);
            var item = new SteamGridDbResultItem(
                new CoverAsset { Source = CoverSource.SteamGridDb, SourceId = "different-source-id-same-image" },
                alreadyAdded: false);

            await viewModel.AddAsync(item);

            Assert.False(item.AlreadyAdded);
            Assert.Single(_repository.GetCovers(_gameId));
            Assert.False(string.IsNullOrEmpty(viewModel.ErrorMessage));
        }
    }
}
