using System;
using System.IO;
using System.Windows;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class NewGameConfigurationServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly FakePlayniteGameService _gameService;
        private readonly PlayniteCoverService _coverService;
        private readonly FakeDialogsFactory _dialogs = new FakeDialogsFactory();
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();

        public NewGameConfigurationServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "NewGameConfigurationServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _gameService = new FakePlayniteGameService();
            _coverService = new PlayniteCoverService(_repository, _gameService, storage, () => _globalSettings, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private NewGameConfigurationService NewService() =>
            new NewGameConfigurationService(_coverService, () => _globalSettings, _dialogs, new FakeCoverShuffleLogger());

        [Fact]
        public void HandleNewGame_WithDoNothing_NeverPromptsOrEnables()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.DoNothing };
            var gameId = Guid.NewGuid();

            NewService().HandleNewGame(gameId, "Some Game");

            Assert.Empty(_dialogs.ShownMessages);
            Assert.False(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void HandleNewGame_WithAutomatic_EnablesWithoutPrompting()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Automatic };
            var gameId = Guid.NewGuid();

            NewService().HandleNewGame(gameId, "Some Game");

            Assert.Empty(_dialogs.ShownMessages);
            Assert.True(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void HandleNewGame_WithAsk_AndUserAccepts_Enables()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Ask };
            _dialogs.NextMessageBoxResult = MessageBoxResult.Yes;
            var gameId = Guid.NewGuid();

            NewService().HandleNewGame(gameId, "Some Game");

            Assert.Single(_dialogs.ShownMessages);
            Assert.True(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void HandleNewGame_WithAsk_AndUserDeclines_DoesNotEnable()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Ask };
            _dialogs.NextMessageBoxResult = MessageBoxResult.No;
            var gameId = Guid.NewGuid();

            NewService().HandleNewGame(gameId, "Some Game");

            Assert.Single(_dialogs.ShownMessages);
            Assert.False(_coverService.IsEnabled(gameId));
        }
    }
}
