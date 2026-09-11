using System;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class CoverShuffleManagerViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly PlayniteCoverService _coverService;
        private readonly CoverShuffleManager _manager;
        private readonly BulkConfigurationService _bulkService;
        private readonly ImportExportService _importExportService;
        private readonly FakeDialogsFactory _dialogs = new FakeDialogsFactory();
        private readonly CoverShuffleManagerViewModel _viewModel;

        public CoverShuffleManagerViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleManagerViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, _gameService, storage, () => new CoverShuffleSettings(), new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _manager = new CoverShuffleManager(_repository, _coverService, _gameService, storage);
            _bulkService = new BulkConfigurationService(_coverService, new FakeCoverShuffleLogger());
            _importExportService = new ImportExportService(_repository, storage, new FakeCoverShuffleLogger());
            _viewModel = new CoverShuffleManagerViewModel(_manager, _bulkService, _importExportService, _dialogs);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void IsEmpty_WhenNoGamesManaged_IsTrue()
        {
            Assert.True(_viewModel.IsEmpty);
            Assert.Empty(_viewModel.Games);
        }

        [Fact]
        public void SearchText_FiltersGamesByName()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Cyberpunk 2077");
            _gameService.SeedGameName(gameB, "Elden Ring");
            _coverService.EnableCoverShuffle(gameA);
            _coverService.EnableCoverShuffle(gameB);
            _viewModel.Reload();

            _viewModel.SearchText = "cyber";

            var row = Assert.Single(_viewModel.Games);
            Assert.Equal("Cyberpunk 2077", row.GameName);
        }

        [Fact]
        public void EnableSelected_EnablesOnlyCheckedGames()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            AddStoredCoverFor(gameB);
            _viewModel.Reload();
            _viewModel.Games.Single(g => g.GameId == gameA).IsSelected = true;

            _viewModel.EnableSelected();

            Assert.True(_coverService.IsEnabled(gameA));
            Assert.False(_coverService.IsEnabled(gameB));
        }

        [Fact]
        public void EnableSelected_WithNoneSelected_SetsAGuidanceStatusMessage()
        {
            _viewModel.EnableSelected();

            Assert.Contains("Select", _viewModel.StatusMessage);
        }

        [Fact]
        public void Export_WithNoFolderSelected_DoesNothing()
        {
            _dialogs.NextSelectedFolder = null;

            _viewModel.Export();

            Assert.Null(_viewModel.StatusMessage);
        }

        [Fact]
        public void Export_WritesToTheSelectedFolder()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCoverFor(gameId);
            var exportFolder = Path.Combine(_tempDirectory, "export");
            _dialogs.NextSelectedFolder = exportFolder;

            _viewModel.Export();

            Assert.True(File.Exists(Path.Combine(exportFolder, "CoverShuffle.json")));
        }

        [Fact]
        public void SelectedGame_IsNullByDefault()
        {
            Assert.Null(_viewModel.SelectedGame);
            Assert.False(_viewModel.HasSelectedGame);
        }

        [Fact]
        public void Reload_PreservesTheOpenDetailPaneGameAcrossReload()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCoverFor(gameId);
            _viewModel.Reload();
            _viewModel.SelectedGame = _viewModel.Games.Single(g => g.GameId == gameId);

            _viewModel.Reload();

            Assert.NotNull(_viewModel.SelectedGame);
            Assert.Equal(gameId, _viewModel.SelectedGame.GameId);
            Assert.True(_viewModel.HasSelectedGame);
        }

        [Fact]
        public void RefreshGameSummary_UpdatesTheRowsCoverCountAndEnabledState_WithoutRebuildingTheList()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCoverFor(gameId);
            _viewModel.Reload();
            var row = _viewModel.Games.Single(g => g.GameId == gameId);
            Assert.Equal(1, row.CoverCount);
            _coverService.EnableCoverShuffle(gameId);

            AddStoredCoverFor(gameId);
            _viewModel.RefreshGameSummary(gameId);

            Assert.Same(row, _viewModel.Games.Single(g => g.GameId == gameId));
            Assert.Equal(2, row.CoverCount);
            Assert.True(row.IsEnabled);
        }

        [Fact]
        public void FilterMode_Enabled_ShowsOnlyEnabledGames()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            _coverService.EnableCoverShuffle(gameA);
            _viewModel.Reload();

            _viewModel.FilterMode = ManagedGameFilterMode.Enabled;

            var row = Assert.Single(_viewModel.Games);
            Assert.Equal(gameA, row.GameId);
        }

        [Fact]
        public void FilterMode_NoCovers_ShowsOnlyGamesWithoutCovers()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            _coverService.EnableCoverShuffle(gameB);
            _viewModel.Reload();

            _viewModel.FilterMode = ManagedGameFilterMode.NoCovers;

            var row = Assert.Single(_viewModel.Games);
            Assert.Equal(gameB, row.GameId);
        }

        [Fact]
        public void SortMode_CoverCount_OrdersDescendingByCoverCount()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            AddStoredCoverFor(gameB);
            AddStoredCoverFor(gameB);
            _viewModel.Reload();

            _viewModel.SortMode = ManagedGameSortMode.CoverCount;

            Assert.Equal(gameB, _viewModel.Games[0].GameId);
            Assert.Equal(gameA, _viewModel.Games[1].GameId);
        }

        [Fact]
        public void SourceFilter_RestrictsToGamesWithACoverFromThatSource()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCoverFor(gameId);
            _viewModel.Reload();

            _viewModel.SourceFilter = CoverSource.SteamGridDb;

            Assert.Empty(_viewModel.Games);

            _viewModel.SourceFilter = CoverSource.LocalFile;

            Assert.Single(_viewModel.Games);
        }

        [Fact]
        public void Dashboard_ReportsCorrectCountsAcrossAMixOfGames()
        {
            var enabledWithCovers = Guid.NewGuid();
            var disabledWithCovers = Guid.NewGuid();
            var enabledNoCovers = Guid.NewGuid();
            _gameService.SeedGameName(enabledWithCovers, "Enabled With Covers");
            _gameService.SeedGameName(disabledWithCovers, "Disabled With Covers");
            _gameService.SeedGameName(enabledNoCovers, "Enabled No Covers");
            AddValidStoredCoverFor(enabledWithCovers);
            AddValidStoredCoverFor(enabledWithCovers);
            AddValidStoredCoverFor(disabledWithCovers);
            _coverService.EnableCoverShuffle(enabledWithCovers);
            _coverService.EnableCoverShuffle(enabledNoCovers);

            _viewModel.Reload();

            Assert.Equal(3, _viewModel.GamesManagedCount);
            Assert.Equal(2, _viewModel.GamesEnabledCount);
            Assert.Equal(3, _viewModel.TotalCoversCount);
            // Only "enabledNoCovers" has zero covers; the other two conditions
            // (disabled, or having covers) are valid configurations, not
            // attention-worthy per the Stage 3 spec.
            Assert.Equal(1, _viewModel.GamesNeedingAttentionCount);
            Assert.True(_viewModel.HasGamesNeedingAttention);
        }

        [Fact]
        public void NeedsAttention_DoesNotTriggerForFewerThanTenCoversOrExactlyOneCoverOrDisabledShuffle()
        {
            var oneCoverDisabled = Guid.NewGuid();
            _gameService.SeedGameName(oneCoverDisabled, "One Cover Disabled");
            AddValidStoredCoverFor(oneCoverDisabled);
            // Deliberately left disabled and with only one (of ten possible)
            // covers - both are valid configurations, not problems.

            _viewModel.Reload();

            var row = Assert.Single(_viewModel.Games);
            Assert.False(row.NeedsAttention);
            Assert.Equal(0, _viewModel.GamesNeedingAttentionCount);
            Assert.False(_viewModel.HasGamesNeedingAttention);
        }

        [Fact]
        public void NeedsAttention_TriggersForAGameWithZeroCoversRegardlessOfEnabledState()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "No Covers, Disabled");
            _repository.SaveGameConfiguration(new PluginCoverShuffle.Domain.GameConfiguration { GameId = gameId });
            // Not enabled, has no covers - still a real "needs attention"
            // condition (a managed game with nothing to shuffle at all).

            _viewModel.Reload();

            var row = Assert.Single(_viewModel.Games);
            Assert.True(row.NeedsAttention);
            Assert.Equal("No covers", row.AttentionReasonText);
        }

        [Fact]
        public void SelectedGamesCount_ReflectsCheckedRowsReactively()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            AddStoredCoverFor(gameB);
            _viewModel.Reload();

            Assert.Equal(0, _viewModel.SelectedGamesCount);

            _viewModel.Games.Single(g => g.GameId == gameA).IsSelected = true;
            Assert.Equal(1, _viewModel.SelectedGamesCount);

            _viewModel.Games.Single(g => g.GameId == gameB).IsSelected = true;
            Assert.Equal(2, _viewModel.SelectedGamesCount);
        }

        [Fact]
        public void ResetOverridesForSelected_ClearsOverridesOnlyForCheckedGames()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            AddStoredCoverFor(gameB);
            _coverService.SetIntervalOverride(gameA, TimeSpan.FromHours(2));
            _coverService.SetIntervalOverride(gameB, TimeSpan.FromHours(2));
            _viewModel.Reload();
            _viewModel.Games.Single(g => g.GameId == gameA).IsSelected = true;

            _viewModel.ResetOverridesForSelected();

            Assert.Null(_repository.GetGameConfiguration(gameA).SettingsOverride);
            Assert.NotNull(_repository.GetGameConfiguration(gameB).SettingsOverride);
        }

        [Fact]
        public void ResetOverridesForSelected_WithNoneSelected_SetsAGuidanceStatusMessage()
        {
            _viewModel.ResetOverridesForSelected();

            Assert.Contains("Select", _viewModel.StatusMessage);
        }

        [Fact]
        public void ShowNoSearchResults_WhenSearchExcludesEveryGame_IsTrue()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Elden Ring");
            AddStoredCoverFor(gameId);
            _viewModel.Reload();

            _viewModel.SearchText = "nonexistent";

            Assert.True(_viewModel.ShowNoSearchResults);
            Assert.False(_viewModel.IsEmpty);
        }

        [Fact]
        public void ShowNoSearchResults_WhenNoGamesManagedAtAll_IsFalse()
        {
            _viewModel.SearchText = "anything";

            Assert.False(_viewModel.ShowNoSearchResults);
            Assert.True(_viewModel.IsEmpty);
        }

        [Fact]
        public void ShowAllHealthy_WhenNeedsAttentionFilterFindsNothing_IsTrue()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Healthy Game");
            AddValidStoredCoverFor(gameId);
            _viewModel.Reload();

            _viewModel.FilterMode = ManagedGameFilterMode.NeedsAttention;

            Assert.True(_viewModel.ShowAllHealthy);
            Assert.False(_viewModel.ShowNoSearchResults);
        }

        [Fact]
        public void ShowAllHealthy_WhenSearchTextIsAlsoSet_PrefersTheSearchEmptyMessage()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Healthy Game");
            AddValidStoredCoverFor(gameId);
            _viewModel.Reload();
            _viewModel.FilterMode = ManagedGameFilterMode.NeedsAttention;

            _viewModel.SearchText = "nothing matches this";

            Assert.False(_viewModel.ShowAllHealthy);
            Assert.True(_viewModel.ShowNoSearchResults);
        }

        private void AddStoredCoverFor(Guid gameId)
        {
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            var storage = new CoverStorage(layout);
            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            var coverId = Guid.NewGuid();
            var relativePath = storage.SaveCoverFile(gameId, coverId, sourceFile);
            _repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            });
        }

        /// <summary>
        /// Same as <see cref="AddStoredCoverFor"/> but writes a real,
        /// decodable PNG rather than garbage bytes, for tests that need a
        /// cover the "Needs Attention" corruption probe genuinely considers
        /// healthy (the garbage-byte helper above is deliberately "corrupt"
        /// by the same real probe, which is exactly what a few other tests
        /// in this file and in CoverShuffleManagerTests rely on).
        /// </summary>
        private void AddValidStoredCoverFor(Guid gameId)
        {
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            var storage = new CoverStorage(layout);
            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new System.Drawing.Bitmap(4, 4))
            {
                bitmap.Save(sourceFile, System.Drawing.Imaging.ImageFormat.Png);
            }

            var coverId = Guid.NewGuid();
            var relativePath = storage.SaveCoverFile(gameId, coverId, sourceFile);
            _repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            });
        }
    }
}
