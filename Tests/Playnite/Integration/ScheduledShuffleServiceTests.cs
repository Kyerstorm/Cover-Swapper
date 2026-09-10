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
    public class ScheduledShuffleServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private readonly ScheduledShuffleService _service;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();

        public ScheduledShuffleServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ScheduledShuffleServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger());
            _service = new ScheduledShuffleService(_repository, _coverService, new FakeCoverShuffleLogger());
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
        public void ShuffleIfDue_ForDisabledGame_DoesNothing()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);

            _service.ShuffleIfDue(gameId);

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void ShuffleIfDue_ForEnabledGameWithNoShuffleStateYet_ShufflesImmediately()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            _service.ShuffleIfDue(gameId);

            Assert.Single(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void ShuffleIfDue_WhenNextShuffleIsInTheFuture_DoesNothing()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);
            _service.ShuffleIfDue(gameId);
            _gameService.SetCoverReferenceCalls.Clear();

            _service.ShuffleIfDue(gameId);

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void ShuffleIfDue_WhenNextShuffleIsInThePast_ShufflesOnceOnly()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);
            _repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                NextShuffleAt = DateTime.UtcNow.AddDays(-10)
            });

            _service.ShuffleIfDue(gameId);
            _service.ShuffleIfDue(gameId);

            // First call shuffles and advances the schedule into the future;
            // the second call must not shuffle again immediately after.
            Assert.Single(_gameService.SetCoverReferenceCalls);
        }
    }
}
