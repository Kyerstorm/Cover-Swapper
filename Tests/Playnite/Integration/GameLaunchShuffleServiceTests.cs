using System;
using System.IO;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class GameLaunchShuffleServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();

        public GameLaunchShuffleServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "GameLaunchShuffleServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private GameLaunchShuffleService NewService(CoverShuffleNotificationService notificationService = null) =>
            new GameLaunchShuffleService(_coverService, new FakeCoverShuffleLogger(), _gameService, notificationService);

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
        public void HandleGameStarting_WithShuffleOnLaunchDisabled_DoesNothing()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            NewService().HandleGameStarting(gameId);

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void HandleGameStarting_WithShuffleOnLaunchEnabled_ShufflesTheCover()
        {
            _globalSettings = new CoverShuffleSettings { ShuffleOnGameLaunch = true };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            NewService().HandleGameStarting(gameId);

            Assert.Single(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void HandleGameStarting_ForDisabledGame_DoesNothingEvenWithShuffleOnLaunchEnabled()
        {
            _globalSettings = new CoverShuffleSettings { ShuffleOnGameLaunch = true };
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);

            NewService().HandleGameStarting(gameId);

            Assert.Empty(_gameService.SetCoverReferenceCalls);
        }

        [Fact]
        public void HandleGameStarting_WithNoCovers_DoesNotThrow()
        {
            _globalSettings = new CoverShuffleSettings { ShuffleOnGameLaunch = true };
            var gameId = Guid.NewGuid();
            _coverService.EnableCoverShuffle(gameId);

            var exception = Record.Exception(() => NewService().HandleGameStarting(gameId));

            Assert.Null(exception);
        }

        [Fact]
        public void HandleGameStarting_WithSuccessfulShuffle_Notifies()
        {
            _globalSettings = new CoverShuffleSettings { ShuffleOnGameLaunch = true };
            var notifications = new FakeNotificationsApi();
            var notificationService = new CoverShuffleNotificationService(notifications);
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            _coverService.EnableCoverShuffle(gameId);

            NewService(notificationService).HandleGameStarting(gameId);

            Assert.Single(notifications.AddedMessages);
            Assert.Equal(NotificationType.Info, notifications.AddedMessages[0].Type);
        }
    }
}
