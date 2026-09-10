using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;
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
        private readonly CoverImportService _importService;
        private readonly FakeCoverProvider _playniteMetadataProvider = new FakeCoverProvider { Source = CoverSource.PlayniteMetadata };
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

        private string CreateValidImageFile()
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        private NewGameConfigurationService NewService() =>
            new NewGameConfigurationService(_coverService, _playniteMetadataProvider, _importService, () => _globalSettings, _dialogs, new FakeCoverShuffleLogger());

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

        [Fact]
        public void HandleNewGame_WithAutomatic_AndExistingPlayniteCover_ImportsItIntoPool()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Automatic };
            var gameId = Guid.NewGuid();
            var coverFilePath = CreateValidImageFile();
            _playniteMetadataProvider.SearchResult = CoverSearchResult.Succeeded(new CoverAsset
            {
                Source = CoverSource.PlayniteMetadata,
                SourceId = "cover",
                FilePath = coverFilePath,
                PreviewUrl = coverFilePath
            });
            _playniteMetadataProvider.DownloadResult = CoverDownloadResult.Succeeded(coverFilePath);

            NewService().HandleNewGame(gameId, "Some Game");

            var covers = _repository.GetCovers(gameId);
            Assert.Single(covers);
            Assert.Equal(CoverSource.PlayniteMetadata, covers[0].Source);
            Assert.Equal(1, _playniteMetadataProvider.DownloadCallCount);
        }

        [Fact]
        public void HandleNewGame_WithAutomatic_AndNoExistingPlayniteArtwork_StillEnablesWithoutError()
        {
            _globalSettings = new CoverShuffleSettings { NewGameBehavior = NewGameBehavior.Automatic };
            _playniteMetadataProvider.SearchResult = CoverSearchResult.Failed("This game has no artwork in Playnite yet.");
            var gameId = Guid.NewGuid();

            var exception = Record.Exception(() => NewService().HandleNewGame(gameId, "Some Game"));

            Assert.Null(exception);
            Assert.True(_coverService.IsEnabled(gameId));
            Assert.Empty(_repository.GetCovers(gameId));
        }
    }
}
