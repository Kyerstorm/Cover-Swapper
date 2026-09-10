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
    public class StartupShuffleServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private readonly ScheduledShuffleService _scheduledShuffleService;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();
        private readonly StartupShuffleService _service;

        public StartupShuffleServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "StartupShuffleServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _scheduledShuffleService = new ScheduledShuffleService(_repository, _coverService, new FakeCoverShuffleLogger());
            _service = new StartupShuffleService(_repository, _scheduledShuffleService, () => _globalSettings, new FakeCoverShuffleLogger());
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
        public void RunDueShuffles_AppliesDueShufflesForEveryKnownEnabledGame()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            _service.RunDueShuffles();

            Assert.Single(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void RunDueShuffles_SkipsGamesThatAreNotEnabled()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            // Never enabled, but has covers - should not be touched.

            _service.RunDueShuffles();

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void RunDueShuffles_WhenShuffleOnStartupDisabled_DoesNothing()
        {
            _globalSettings = new CoverShuffleSettings { ShuffleOnStartup = false };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            _service.RunDueShuffles();

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void RunDueShuffles_OneGameThrowing_DoesNotStopOthersFromBeingChecked()
        {
            var brokenGameId = Guid.NewGuid();
            var healthyGameId = Guid.NewGuid();
            AddStoredCover(brokenGameId);
            AddStoredCover(healthyGameId);
            _coverService.EnableCoverShuffle(brokenGameId);
            _coverService.EnableCoverShuffle(healthyGameId);
            _gameService.GameIdToThrowOn = brokenGameId;

            _service.RunDueShuffles();

            // The healthy game must still have been shuffled even though
            // applying the broken game's cover threw unexpectedly.
            Assert.Contains(healthyGameId, _gameService.SetCoverReferenceCalls.ConvertAll(c => c.GameId));
        }
    }
}
