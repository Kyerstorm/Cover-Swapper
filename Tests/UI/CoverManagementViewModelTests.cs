using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
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
    public class CoverManagementViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverImportService _importService;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private readonly BulkConfigurationService _bulkConfigurationService;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();

        public CoverManagementViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverManagementViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger());
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(
                _repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _bulkConfigurationService = new BulkConfigurationService(_coverService, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private Cover ImportCover(Guid gameId)
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }

            var result = _importService.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });
            Assert.True(result.IsSuccess);
            return result.Cover;
        }

        private CoverManagementViewModel CreateViewModel(Guid gameId, string gameName = null)
        {
            return new CoverManagementViewModel(gameId, gameName, _repository, _storage, _coverService, _bulkConfigurationService);
        }

        [Fact]
        public void Constructor_LoadsExistingCoversForTheGame()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);

            var viewModel = CreateViewModel(gameId);

            Assert.Single(viewModel.Covers);
            Assert.Equal(cover.CoverId, viewModel.Covers[0].CoverId);
            Assert.Equal(_storage.GetAbsolutePath(cover.LocalPath), viewModel.Covers[0].AbsoluteImagePath);
        }

        [Fact]
        public void Remove_TakesCoverOutOfThePool_ButLeavesTheFileOnDisk()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.Remove(cover.CoverId);

            Assert.Empty(viewModel.Covers);
            Assert.Empty(_repository.GetCovers(gameId));
            Assert.True(_storage.CoverFileExists(cover.LocalPath));
        }

        [Fact]
        public void Constructor_WithNoGameName_FallsBackToPlaceholderText()
        {
            var viewModel = CreateViewModel(Guid.NewGuid(), null);

            Assert.Equal("(game not found in Playnite)", viewModel.GameName);
        }

        [Fact]
        public void Constructor_ForDisabledGame_ReflectsDisabledStatus()
        {
            var gameId = Guid.NewGuid();

            var viewModel = CreateViewModel(gameId);

            Assert.False(viewModel.IsEnabled);
            Assert.Equal("Disabled", viewModel.StatusText);
            Assert.Equal("Enable", viewModel.ToggleEnabledButtonText);
            Assert.Equal("Not scheduled (disabled)", viewModel.NextShuffleText);
        }

        [Fact]
        public void ToggleEnabled_OnADisabledGame_EnablesItAndCapturesOriginal()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original.png");
            var viewModel = CreateViewModel(gameId);

            viewModel.ToggleEnabled();

            Assert.True(viewModel.IsEnabled);
            Assert.Equal("Enabled", viewModel.StatusText);
            Assert.True(_coverService.HasSavedOriginalCover(gameId));
        }

        [Fact]
        public void ToggleEnabled_OnAnEnabledGame_DisablesIt()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);
            viewModel.ToggleEnabled();

            viewModel.ToggleEnabled();

            Assert.False(viewModel.IsEnabled);
        }

        [Fact]
        public void SetInterval_PersistsAPerGameOverride_ReflectedOnReload()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);

            viewModel.SetInterval(TimeSpan.FromHours(6));

            Assert.Equal(6, viewModel.CurrentIntervalHours);
            Assert.Equal(TimeSpan.FromHours(6), _coverService.GetEffectiveInterval(gameId));
        }

        [Fact]
        public void SetInterval_WithANonPositiveValue_IsIgnored()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);
            var before = viewModel.CurrentIntervalHours;

            viewModel.SetInterval(TimeSpan.Zero);

            Assert.Equal(before, viewModel.CurrentIntervalHours);
        }

        [Fact]
        public void ShuffleNow_AppliesACover_AndMarksItCurrent()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.ShuffleNow();

            Assert.Equal("Shuffled to a new cover.", viewModel.StatusMessage);
            Assert.True(viewModel.Covers.Single(c => c.CoverId == cover.CoverId).IsCurrent);
        }

        [Fact]
        public void ShuffleNow_WithNoCovers_ShowsTheFailureMessage()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);

            viewModel.ShuffleNow();

            Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
            Assert.NotEqual("Shuffled to a new cover.", viewModel.StatusMessage);
        }

        [Fact]
        public void RestoreOriginal_RestoresTheCapturedCover()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original.png");
            var viewModel = CreateViewModel(gameId);
            viewModel.ToggleEnabled();
            _gameService.SetCoverReference(gameId, "shuffled.png");

            viewModel.RestoreOriginal();

            Assert.Equal("original.png", _gameService.GetCoverReference(gameId));
        }
    }
}
