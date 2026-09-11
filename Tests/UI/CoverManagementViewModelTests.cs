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
            _importService = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger(), new ImageNormalizationService());
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

        private Cover ImportCover(Guid gameId, System.Drawing.Color? fillColor = null)
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                if (fillColor.HasValue)
                {
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(fillColor.Value);
                    }
                }

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
        public void Reload_WhenCoverFileIsMissing_FlagsItAsMissing()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var viewModel = CreateViewModel(gameId);

            Assert.True(viewModel.Covers.Single().IsFileMissing);
        }

        [Fact]
        public void Reload_WhenCoverFilePresent_IsNotFlaggedAsMissing()
        {
            var gameId = Guid.NewGuid();
            ImportCover(gameId);

            var viewModel = CreateViewModel(gameId);

            Assert.False(viewModel.Covers.Single().IsFileMissing);
        }

        [Fact]
        public void Reload_AssignsCoverNumberByAddedOrder()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);

            var viewModel = CreateViewModel(gameId);

            Assert.Equal(1, viewModel.Covers.Single(c => c.CoverId == first.CoverId).CoverNumber);
            Assert.Equal(2, viewModel.Covers.Single(c => c.CoverId == second.CoverId).CoverNumber);
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
        public void ChooseCover_AppliesTheSpecificCover_AndMarksItCurrent()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);

            viewModel.ChooseCover(second.CoverId);

            Assert.Equal("Cover applied.", viewModel.StatusMessage);
            Assert.True(viewModel.Covers.Single(c => c.CoverId == second.CoverId).IsCurrent);
            Assert.False(viewModel.Covers.Single(c => c.CoverId == first.CoverId).IsCurrent);
        }

        [Fact]
        public void ChooseCover_ForACoverNotInThePool_ShowsTheFailureMessage()
        {
            var gameId = Guid.NewGuid();
            ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.ChooseCover(Guid.NewGuid());

            Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
            Assert.NotEqual("Cover applied.", viewModel.StatusMessage);
        }

        [Fact]
        public void ResetToGlobalDefaults_ClearsOverrides_AndReflectsCurrentGlobalValues()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = false, Interval = TimeSpan.FromHours(24) };
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);
            viewModel.ToggleEnabled();
            viewModel.SetInterval(TimeSpan.FromHours(3));
            Assert.True(viewModel.HasAnyOverride);
            Assert.True(viewModel.IsIntervalOverridden);

            viewModel.ResetToGlobalDefaults();

            Assert.False(viewModel.IsEnabled);
            Assert.Equal(24, viewModel.CurrentIntervalHours);
            Assert.False(viewModel.HasAnyOverride);
            Assert.False(viewModel.IsIntervalOverridden);
        }

        [Fact]
        public void IsEnabledOverridden_DefaultsToFalse_FollowingTheGlobalDefault()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);

            Assert.False(viewModel.IsEnabledOverridden);
            Assert.Equal("(using global default)", viewModel.EnabledSourceText);
        }

        [Fact]
        public void IsEnabledOverridden_TrueAfterTogglingEnabled_AndClearedByResetToGlobalDefaults()
        {
            var gameId = Guid.NewGuid();
            var viewModel = CreateViewModel(gameId);

            viewModel.ToggleEnabled();

            Assert.True(viewModel.IsEnabledOverridden);
            Assert.Equal("(custom)", viewModel.EnabledSourceText);

            viewModel.ResetToGlobalDefaults();

            Assert.False(viewModel.IsEnabledOverridden);
        }

        [Fact]
        public void SelectedCover_IsNullByDefault()
        {
            var gameId = Guid.NewGuid();
            ImportCover(gameId);

            var viewModel = CreateViewModel(gameId);

            Assert.Null(viewModel.SelectedCover);
            Assert.False(viewModel.HasSelectedCover);
        }

        [Fact]
        public void SelectedCover_WhenSetToACoverThatIsNotCurrentOrMissing_AllowsApply()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == cover.CoverId);

            Assert.True(viewModel.HasSelectedCover);
            Assert.True(viewModel.CanApplySelectedCover);
        }

        [Fact]
        public void SelectedCover_WhenAlreadyCurrent_DisallowsApply()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            viewModel.ChooseCover(cover.CoverId);

            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == cover.CoverId);

            Assert.False(viewModel.CanApplySelectedCover);
        }

        [Fact]
        public void ChooseSelectedCover_AppliesTheSelectedCover()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == second.CoverId);

            viewModel.ChooseSelectedCover();

            Assert.True(viewModel.Covers.Single(c => c.CoverId == second.CoverId).IsCurrent);
            Assert.False(viewModel.Covers.Single(c => c.CoverId == first.CoverId).IsCurrent);
        }

        [Fact]
        public void ChooseSelectedCover_WithNoSelection_DoesNothing()
        {
            var gameId = Guid.NewGuid();
            ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.ChooseSelectedCover();

            Assert.Null(viewModel.StatusMessage);
        }

        [Fact]
        public void RemoveSelectedCover_RemovesTheSelectedCover_ButLeavesTheFileOnDisk()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == cover.CoverId);

            viewModel.RemoveSelectedCover();

            Assert.Empty(viewModel.Covers);
            Assert.True(_storage.CoverFileExists(cover.LocalPath));
        }

        [Fact]
        public void Reload_PreservesSelectionAcrossReloadWhenTheSelectedCoverStillExists()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == second.CoverId);

            viewModel.Reload();

            Assert.NotNull(viewModel.SelectedCover);
            Assert.Equal(second.CoverId, viewModel.SelectedCover.CoverId);
        }

        [Fact]
        public void Reload_WhenTheSelectedCoverWasRemoved_ClearsSelectionInsteadOfPointingAtAStaleItem()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == cover.CoverId);

            _repository.RemoveCover(gameId, cover.CoverId);
            viewModel.Reload();

            Assert.Null(viewModel.SelectedCover);
        }

        [Fact]
        public void CurrentCover_ReflectsWhicheverCoverWasLastApplied()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            Assert.Null(viewModel.CurrentCover);
            Assert.False(viewModel.HasCurrentCover);

            viewModel.ChooseCover(cover.CoverId);

            Assert.NotNull(viewModel.CurrentCover);
            Assert.Equal(cover.CoverId, viewModel.CurrentCover.CoverId);
            Assert.True(viewModel.HasCurrentCover);
        }

        [Fact]
        public void CanRestoreSelectedFromSteamGridDb_OnlyOfferedForMissingSteamGridDbCovers()
        {
            var gameId = Guid.NewGuid();
            var localCover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            viewModel.SelectedCover = viewModel.Covers.Single(c => c.CoverId == localCover.CoverId);

            Assert.False(viewModel.CanRestoreSelectedFromSteamGridDb);
        }

        [Fact]
        public void ToggleFavorite_TogglesTheFlag_AndPersistsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            Assert.False(viewModel.Covers.Single().IsFavorite);

            viewModel.ToggleFavorite(cover.CoverId);

            Assert.True(viewModel.Covers.Single().IsFavorite);

            var reloaded = CreateViewModel(gameId);
            Assert.True(reloaded.Covers.Single().IsFavorite);
        }

        [Fact]
        public void ToggleFavorite_DoesNotAffectShuffleEligibility()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.ToggleFavorite(cover.CoverId);

            Assert.True(_repository.GetCover(gameId, cover.CoverId).IsEnabled);
        }

        [Fact]
        public void ToggleCoverEnabled_DisablesTheCover_LeavingItInThePool()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            Assert.True(viewModel.Covers.Single().IsCoverEnabled);

            viewModel.ToggleCoverEnabled(cover.CoverId);

            Assert.False(viewModel.Covers.Single().IsCoverEnabled);
            Assert.Single(_repository.GetCovers(gameId));
        }

        [Fact]
        public void ToggleCoverEnabled_TwiceReturnsToEnabled()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);

            viewModel.ToggleCoverEnabled(cover.CoverId);
            viewModel.ToggleCoverEnabled(cover.CoverId);

            Assert.True(viewModel.Covers.Single().IsCoverEnabled);
        }

        [Fact]
        public void ToggleCoverEnabled_OnTheCurrentCover_LeavesItStillCurrent()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = CreateViewModel(gameId);
            viewModel.ChooseCover(cover.CoverId);

            viewModel.ToggleCoverEnabled(cover.CoverId);

            var displayed = viewModel.Covers.Single();
            Assert.True(displayed.IsCurrent);
            Assert.False(displayed.IsCoverEnabled);
        }

        [Fact]
        public void SetSelection_TracksTheMultiSelection()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);

            viewModel.SetSelection(viewModel.Covers);

            Assert.Equal(2, viewModel.SelectedCoverCount);
            Assert.True(viewModel.HasMultipleSelectedCovers);
        }

        [Fact]
        public void EnableSelectedCovers_EnablesEveryDisabledCoverInTheSelection()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.ToggleCoverEnabled(first.CoverId);
            viewModel.ToggleCoverEnabled(second.CoverId);
            viewModel.SetSelection(viewModel.Covers);

            viewModel.EnableSelectedCovers();

            Assert.All(viewModel.Covers, c => Assert.True(c.IsCoverEnabled));
        }

        [Fact]
        public void DisableSelectedCovers_DisablesEveryEnabledCoverInTheSelection_WithoutDeletingAnything()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.SetSelection(viewModel.Covers);

            viewModel.DisableSelectedCovers();

            Assert.All(viewModel.Covers, c => Assert.False(c.IsCoverEnabled));
            Assert.Equal(2, _repository.GetCovers(gameId).Count);
            Assert.True(_storage.CoverFileExists(first.LocalPath));
            Assert.True(_storage.CoverFileExists(second.LocalPath));
        }

        [Fact]
        public void RemoveSelectedCovers_RemovesEveryCoverInTheSelection_ButLeavesFilesOnDisk()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.SetSelection(viewModel.Covers);

            viewModel.RemoveSelectedCovers();

            Assert.Empty(viewModel.Covers);
            Assert.Empty(_repository.GetCovers(gameId));
            Assert.True(_storage.CoverFileExists(first.LocalPath));
            Assert.True(_storage.CoverFileExists(second.LocalPath));
        }

        [Fact]
        public void RemoveSelectedCovers_NeverTouchesCoversOutsideTheSelection()
        {
            var gameId = Guid.NewGuid();
            var first = ImportCover(gameId, System.Drawing.Color.Red);
            var second = ImportCover(gameId, System.Drawing.Color.Blue);
            var viewModel = CreateViewModel(gameId);
            viewModel.SetSelection(viewModel.Covers.Where(c => c.CoverId == first.CoverId));

            viewModel.RemoveSelectedCovers();

            Assert.Single(_repository.GetCovers(gameId));
            Assert.Equal(second.CoverId, _repository.GetCovers(gameId).Single().CoverId);
        }

        [Fact]
        public void Reload_WhenCoverFileIsCorrupt_FlagsItAsCorruptButNotMissing()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var absolutePath = _storage.GetAbsolutePath(cover.LocalPath);
            File.WriteAllBytes(absolutePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var viewModel = CreateViewModel(gameId);

            var displayed = viewModel.Covers.Single();
            Assert.False(displayed.IsFileMissing);
            Assert.True(displayed.IsCorrupt);
            Assert.True(displayed.IsUnavailable);
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
