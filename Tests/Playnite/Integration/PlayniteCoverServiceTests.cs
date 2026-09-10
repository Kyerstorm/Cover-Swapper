using System;
using System.IO;
using PluginCoverShuffle.Domain;
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

        public PlayniteCoverServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShufflePlayniteCoverServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _service = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger());
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
                SettingsOverride = new CoverShuffleSettings { Interval = TimeSpan.FromHours(2) }
            });
            var before = DateTime.UtcNow;

            _service.ShuffleToNextCover(gameId);

            var state = _repository.GetShuffleState(gameId);
            Assert.InRange(state.NextShuffleAt.Value, before.AddHours(2).AddMinutes(-1), before.AddHours(2).AddMinutes(1));
        }
    }
}
