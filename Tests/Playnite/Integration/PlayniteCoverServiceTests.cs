using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class PlayniteCoverServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _service;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();
        private IShuffleEngine _shuffleEngine = new ShuffleEngine(new FakeShuffleRandomizer());

        public PlayniteCoverServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShufflePlayniteCoverServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _service = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger(), _shuffleEngine);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private Cover AddStoredCover(Guid gameId)
        {
            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });

            var coverId = Guid.NewGuid();
            var relativePath = _storage.SaveCoverFile(gameId, coverId, sourceFile);
            var cover = new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            };
            _repository.AddCover(cover);
            return cover;
        }

        [Fact]
        public void IsEnabled_ForUnknownGame_DefaultsToFalse()
        {
            Assert.False(_service.IsEnabled(Guid.NewGuid()));
        }

        [Fact]
        public void EnableCoverShuffle_MarksGameEnabled_AndCapturesOriginalCover()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");

            _service.EnableCoverShuffle(gameId);

            Assert.True(_service.IsEnabled(gameId));
            Assert.True(_service.HasSavedOriginalCover(gameId));

            var saved = _repository.GetOriginalArtwork(gameId);
            Assert.Equal("original-cover.png", saved.OriginalCoverReference);
        }

        [Fact]
        public void EnableCoverShuffle_CalledTwice_DoesNotOverwriteAlreadyCapturedOriginal()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");

            _service.EnableCoverShuffle(gameId);

            // Simulate a shuffle having changed the cover in the meantime.
            _gameService.SetCoverReference(gameId, "some-shuffled-cover.png");

            _service.EnableCoverShuffle(gameId);

            var saved = _repository.GetOriginalArtwork(gameId);
            Assert.Equal("original-cover.png", saved.OriginalCoverReference);
        }

        [Fact]
        public void DisableCoverShuffle_MarksGameDisabled_AndDoesNotChangeCover()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            _service.EnableCoverShuffle(gameId);

            _service.DisableCoverShuffle(gameId);

            Assert.False(_service.IsEnabled(gameId));
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void RestoreOriginalCover_SetsGameCoverBackToCapturedOriginal()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            _service.EnableCoverShuffle(gameId);
            _gameService.SetCoverReference(gameId, "some-shuffled-cover.png");

            _service.RestoreOriginalCover(gameId);

            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void RestoreOriginalCover_WithNoSavedOriginal_DoesNotThrowOrChangeCover()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "whatever-is-there.png");

            _service.RestoreOriginalCover(gameId);

            Assert.Equal("whatever-is-there.png", _gameService.GetCoverReference(gameId));
            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void EnableThenDisableThenRestore_RoundTripsSafely()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");

            _service.EnableCoverShuffle(gameId);
            _gameService.SetCoverReference(gameId, "shuffled-cover.png");
            _service.DisableCoverShuffle(gameId);
            _service.RestoreOriginalCover(gameId);

            Assert.False(_service.IsEnabled(gameId));
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void ShuffleToNextCover_WithNoCovers_FailsWithoutChangingAnything()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");

            var result = _service.ShuffleToNextCover(gameId);

            Assert.False(result.Success);
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void ShuffleToNextCover_AppliesACover_AndCapturesOriginalFirst()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);

            var result = _service.ShuffleToNextCover(gameId);

            Assert.True(result.Success);
            Assert.Equal(_storage.GetAbsolutePath(cover.LocalPath), _gameService.GetCoverReference(gameId));
            Assert.True(_service.HasSavedOriginalCover(gameId));
            Assert.Equal("original-cover.png", _repository.GetOriginalArtwork(gameId).OriginalCoverReference);
        }

        [Fact]
        public void ShuffleToNextCover_CyclesThroughAllCoversThenWrapsAround()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var first = AddStoredCover(gameId);
            var second = AddStoredCover(gameId);

            var afterFirstShuffle = _service.ShuffleToNextCover(gameId);
            var afterSecondShuffle = _service.ShuffleToNextCover(gameId);
            var afterThirdShuffle = _service.ShuffleToNextCover(gameId);

            Assert.True(afterFirstShuffle.Success && afterSecondShuffle.Success && afterThirdShuffle.Success);
            Assert.Equal(_storage.GetAbsolutePath(first.LocalPath), _gameService.GetCoverReference(gameId));

            var state = _repository.GetShuffleState(gameId);
            Assert.Equal(first.CoverId, state.CurrentCoverId);
        }

        [Fact]
        public void ShuffleToNextCover_TracksUsageOnTheAppliedCover()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);

            _service.ShuffleToNextCover(gameId);

            var updated = _repository.GetCover(gameId, cover.CoverId);
            Assert.Equal(1, updated.UsageCount);
            Assert.NotNull(updated.LastUsedAt);
        }

        [Fact]
        public void ShuffleToNextCover_AdvancesNextShuffleAt_UsingGlobalDefaultInterval()
        {
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(6) };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            var before = DateTime.UtcNow;

            _service.ShuffleToNextCover(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.NotNull(state.NextShuffleAt);
            Assert.InRange(state.NextShuffleAt.Value, before.AddHours(6).AddMinutes(-1), before.AddHours(6).AddMinutes(1));
        }

        [Fact]
        public void ShuffleToNextCover_UsesPerGameIntervalOverride_WhenPresent()
        {
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(24) };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _repository.SaveGameConfiguration(new GameConfiguration
            {
                GameId = gameId,
                SettingsOverride = new GameSettingsOverride { Interval = TimeSpan.FromHours(2) }
            });
            var before = DateTime.UtcNow;

            _service.ShuffleToNextCover(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.InRange(state.NextShuffleAt.Value, before.AddHours(2).AddMinutes(-1), before.AddHours(2).AddMinutes(1));
        }

        [Fact]
        public void ShuffleToNextCover_WithOneCoverAndItsFileDeleted_FailsWithoutApplyingBrokenReference()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var result = _service.ShuffleToNextCover(gameId);

            Assert.False(result.Success);
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void EnableCoverShuffle_ForGameWithNoOverrideYet_DoesNotResetUnrelatedSettings()
        {
            _globalSettings = new CoverShuffleSettings
            {
                Interval = TimeSpan.FromHours(3),
                NotificationPreference = NotificationPreference.Silent
            };
            var gameId = Guid.NewGuid();

            _service.EnableCoverShuffle(gameId);

            // Enabling must not silently reset unrelated settings (interval,
            // notification preference, ...) back to type defaults for a game
            // that never had its own override before.
            Assert.Equal(TimeSpan.FromHours(3), _service.GetEffectiveInterval(gameId));
            Assert.Equal(NotificationPreference.Silent, _service.GetEffectiveNotificationPreference(gameId));
        }

        [Fact]
        public void EnableCoverShuffle_OnlyOverridesEnabled_OtherSettingsKeepTrackingGlobalChangesLive()
        {
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(3) };
            var gameId = Guid.NewGuid();

            _service.EnableCoverShuffle(gameId);
            Assert.Equal(TimeSpan.FromHours(3), _service.GetEffectiveInterval(gameId));

            // Changing the global interval afterwards must still propagate to
            // this game, since only "Enabled" was ever explicitly overridden.
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(9) };
            Assert.Equal(TimeSpan.FromHours(9), _service.GetEffectiveInterval(gameId));
            Assert.True(_service.IsEnabled(gameId));
        }

        [Fact]
        public void IsEnabled_ForNeverConfiguredGame_ReflectsCurrentGlobalDefault()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();

            Assert.True(_service.IsEnabled(gameId));

            _globalSettings = new CoverShuffleSettings { Enabled = false };
            Assert.False(_service.IsEnabled(gameId));
        }

        [Fact]
        public void ResetOverridesToGlobalDefaults_ClearsOverride_AndGameFollowsCurrentGlobalValues()
        {
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(3), Enabled = false };
            var gameId = Guid.NewGuid();
            _service.EnableCoverShuffle(gameId);
            _service.SetIntervalOverride(gameId, TimeSpan.FromHours(1));
            Assert.True(_service.IsEnabled(gameId));
            Assert.Equal(TimeSpan.FromHours(1), _service.GetEffectiveInterval(gameId));

            _service.ResetOverridesToGlobalDefaults(gameId);

            Assert.False(_service.IsEnabled(gameId));
            Assert.Equal(TimeSpan.FromHours(3), _service.GetEffectiveInterval(gameId));
        }

        [Fact]
        public void ResetOverridesToGlobalDefaults_ForGameWithNoConfiguration_DoesNotThrow()
        {
            _service.ResetOverridesToGlobalDefaults(Guid.NewGuid());
        }

        [Fact]
        public void SetIntervalOverride_ForGameWithNoOverrideYet_DoesNotDisableIt()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();

            _service.SetIntervalOverride(gameId, TimeSpan.FromHours(5));

            // A game following the global "enabled" default must not become
            // disabled just because a bulk action gave it its own interval.
            Assert.True(_service.IsEnabled(gameId));
            Assert.Equal(TimeSpan.FromHours(5), _service.GetEffectiveInterval(gameId));
        }

        [Fact]
        public void ShuffleToNextCover_WithOneCoverFileMissingAndAnotherPresent_SkipsMissingAndAppliesTheOther()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var missing = AddStoredCover(gameId);
            var present = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(missing.LocalPath));

            var result = _service.ShuffleToNextCover(gameId);

            Assert.True(result.Success);
            Assert.Equal(_storage.GetAbsolutePath(present.LocalPath), _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void ShuffleToNextCover_RecordsRandomAsTheShuffleTrigger()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);

            _service.ShuffleToNextCover(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.Equal(ShuffleTrigger.Random, state.LastShuffleTrigger);
        }

        [Fact]
        public void ChooseCover_AppliesTheRequestedCover_AndCapturesOriginalFirst()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var first = AddStoredCover(gameId);
            var second = AddStoredCover(gameId);

            var result = _service.ChooseCover(gameId, second.CoverId);

            Assert.True(result.Success);
            Assert.Equal(_storage.GetAbsolutePath(second.LocalPath), _gameService.GetCoverReference(gameId));
            Assert.True(_service.HasSavedOriginalCover(gameId));
        }

        [Fact]
        public void ChooseCover_TracksUsageAndRecordsManualAsTheShuffleTrigger()
        {
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);

            _service.ChooseCover(gameId, cover.CoverId);

            var updatedCover = _repository.GetCover(gameId, cover.CoverId);
            Assert.Equal(1, updatedCover.UsageCount);
            Assert.NotNull(updatedCover.LastUsedAt);

            var state = _repository.GetShuffleState(gameId);
            Assert.Equal(ShuffleTrigger.Manual, state.LastShuffleTrigger);
            Assert.Equal(cover.CoverId, state.CurrentCoverId);
        }

        [Fact]
        public void ChooseCover_AdvancesNextShuffleAt_UsingEffectiveInterval()
        {
            _globalSettings = new CoverShuffleSettings { Interval = TimeSpan.FromHours(6) };
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);
            var before = DateTime.UtcNow;

            _service.ChooseCover(gameId, cover.CoverId);

            var state = _repository.GetShuffleState(gameId);
            Assert.InRange(state.NextShuffleAt.Value, before.AddHours(6).AddMinutes(-1), before.AddHours(6).AddMinutes(1));
        }

        [Fact]
        public void ChooseCover_ForACoverNotInThePool_FailsWithoutChangingAnything()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            AddStoredCover(gameId);

            var result = _service.ChooseCover(gameId, Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void ChooseCover_WithTheCoversFileMissing_FailsWithoutApplyingBrokenReference()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var result = _service.ChooseCover(gameId, cover.CoverId);

            Assert.False(result.Success);
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_FirstValidCover_AppliesItAndReturnsTrue()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.True(applied);
            Assert.Equal(_storage.GetAbsolutePath(cover.LocalPath), _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_RecordsInitialAsTheShuffleTrigger()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);

            _service.TryApplyInitialShuffle(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.Equal(ShuffleTrigger.Initial, state.LastShuffleTrigger);
        }

        [Fact]
        public void TryApplyInitialShuffle_CreatesFullShuffleState()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true, Interval = TimeSpan.FromHours(6) };
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);
            var before = DateTime.UtcNow;

            _service.TryApplyInitialShuffle(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.Equal(cover.CoverId, state.CurrentCoverId);
            Assert.NotNull(state.LastShuffleAt);
            Assert.InRange(state.NextShuffleAt.Value, before.AddHours(6).AddMinutes(-1), before.AddHours(6).AddMinutes(1));
            Assert.NotNull(state.ShuffleCycle);
        }

        [Fact]
        public void TryApplyInitialShuffle_CapturesOriginalCoverFirst()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            AddStoredCover(gameId);

            _service.TryApplyInitialShuffle(gameId);

            Assert.True(_service.HasSavedOriginalCover(gameId));
            Assert.Equal("original-cover.png", _repository.GetOriginalArtwork(gameId).OriginalCoverReference);
        }

        [Fact]
        public void TryApplyInitialShuffle_TracksUsageOnTheAppliedCover()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);

            _service.TryApplyInitialShuffle(gameId);

            var updated = _repository.GetCover(gameId, cover.CoverId);
            Assert.Equal(1, updated.UsageCount);
        }

        [Fact]
        public void TryApplyInitialShuffle_SecondCover_DoesNotChangeTheAppliedCover()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var first = AddStoredCover(gameId);

            var firstApplied = _service.TryApplyInitialShuffle(gameId);
            var second = AddStoredCover(gameId);
            var secondApplied = _service.TryApplyInitialShuffle(gameId);

            Assert.True(firstApplied);
            Assert.False(secondApplied);
            Assert.Equal(_storage.GetAbsolutePath(first.LocalPath), _gameService.GetCoverReference(gameId));
            Assert.Equal(first.CoverId, _repository.GetShuffleState(gameId).CurrentCoverId);
        }

        [Fact]
        public void TryApplyInitialShuffle_ThirdCover_StillDoesNotChangeTheAppliedCover()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var first = AddStoredCover(gameId);
            _service.TryApplyInitialShuffle(gameId);
            AddStoredCover(gameId);
            _service.TryApplyInitialShuffle(gameId);

            AddStoredCover(gameId);
            var thirdApplied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(thirdApplied);
            Assert.Equal(first.CoverId, _repository.GetShuffleState(gameId).CurrentCoverId);
        }

        [Fact]
        public void TryApplyInitialShuffle_WhenAShuffleStateAlreadyExists_DoesNothing()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var first = AddStoredCover(gameId);
            _service.ShuffleToNextCover(gameId);
            var stateBefore = _repository.GetShuffleState(gameId);
            AddStoredCover(gameId);

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(applied);
            Assert.Equal(stateBefore.CurrentCoverId, _repository.GetShuffleState(gameId).CurrentCoverId);
            Assert.Equal(ShuffleTrigger.Random, _repository.GetShuffleState(gameId).LastShuffleTrigger);
        }

        [Fact]
        public void TryApplyInitialShuffle_WhenCoverShuffleDisabled_DoesNothing()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = false };
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            AddStoredCover(gameId);

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(applied);
            Assert.Null(_repository.GetShuffleState(gameId));
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_WithNoCovers_DoesNothing()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(applied);
            Assert.Null(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_WithOnlyDisabledCovers_DoesNothing()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);
            cover.IsEnabled = false;
            _repository.UpdateCover(cover);

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(applied);
            Assert.Null(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_WithOnlyAMissingCoverFile_DoesNothing_AndDoesNotCreateState()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            _gameService.SeedCoverReference(gameId, "original-cover.png");
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.False(applied);
            Assert.Null(_repository.GetShuffleState(gameId));
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void TryApplyInitialShuffle_WithOneCoverMissingAndAnotherPresent_AppliesThePresentOne()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = true };
            var gameId = Guid.NewGuid();
            var missing = AddStoredCover(gameId);
            var present = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(missing.LocalPath));

            var applied = _service.TryApplyInitialShuffle(gameId);

            Assert.True(applied);
            Assert.Equal(_storage.GetAbsolutePath(present.LocalPath), _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void ChooseCover_RemovesTheChosenCoverFromTheShuffleCycle_SoItIsNotImmediatelyRepeated()
        {
            var gameId = Guid.NewGuid();
            var first = AddStoredCover(gameId);
            var second = AddStoredCover(gameId);

            // Prime a shuffle cycle so both covers are queued.
            _service.ShuffleToNextCover(gameId);
            var stateBeforeChoose = _repository.GetShuffleState(gameId);
            var otherCover = stateBeforeChoose.CurrentCoverId == first.CoverId ? second : first;

            Assert.Contains(otherCover.CoverId, stateBeforeChoose.ShuffleCycle);

            _service.ChooseCover(gameId, otherCover.CoverId);

            var stateAfterChoose = _repository.GetShuffleState(gameId);
            Assert.DoesNotContain(otherCover.CoverId, stateAfterChoose.ShuffleCycle);
        }
    }
}
