using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class GameInstallationServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private readonly CoverImportService _importService;
        private readonly FakeCoverProvider _playniteMetadataProvider = new FakeCoverProvider { Source = CoverSource.PlayniteMetadata };
        private readonly FakeDialogsFactory _dialogs = new FakeDialogsFactory();
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Automatic };

        public GameInstallationServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "GameInstallationServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(_repository, _gameService, storage, () => _globalSettings, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private GameInstallationService NewService()
        {
            var newGameConfigurationService = new NewGameConfigurationService(_coverService, _playniteMetadataProvider, _importService, () => _globalSettings, _dialogs, new FakeCoverShuffleLogger());
            return new GameInstallationService(_repository, newGameConfigurationService, new FakeCoverShuffleLogger());
        }

        [Fact]
        public void HandleGameInstalled_ForGenuinelyNewGame_AppliesConfiguredBehavior()
        {
            var gameId = Guid.NewGuid();

            NewService().HandleGameInstalled(gameId, "Some Game");

            Assert.True(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void HandleGameInstalled_ForAlreadyKnownGame_DoesNotReapplyBehavior()
        {
            var gameId = Guid.NewGuid();
            // Game already has an explicit configuration (e.g. the user
            // manually disabled it) - a reinstall must not override that.
            _coverService.EnableCoverShuffle(gameId);
            _coverService.DisableCoverShuffle(gameId);
            Assert.False(_coverService.IsEnabled(gameId));

            NewService().HandleGameInstalled(gameId, "Some Game");

            Assert.False(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void HandleGameInstalled_WhenNewGameConfigurationThrows_DoesNotPropagate()
        {
            var gameId = Guid.NewGuid();
            var throwingService = new NewGameConfigurationService(
                _coverService,
                _playniteMetadataProvider,
                _importService,
                () => throw new InvalidOperationException("boom"),
                _dialogs,
                new FakeCoverShuffleLogger());
            var service = new GameInstallationService(_repository, throwingService, new FakeCoverShuffleLogger());

            var exception = Record.Exception(() => service.HandleGameInstalled(gameId, "Some Game"));

            Assert.Null(exception);
        }
    }
}
