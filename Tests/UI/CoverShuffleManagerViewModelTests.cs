using System;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
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
    public class CoverShuffleManagerViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly PlayniteCoverService _coverService;
        private readonly CoverShuffleManager _manager;
        private readonly BulkConfigurationService _bulkService;
        private readonly ImportExportService _importExportService;
        private readonly FakeDialogsFactory _dialogs = new FakeDialogsFactory();
        private readonly CoverShuffleManagerViewModel _viewModel;

        public CoverShuffleManagerViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleManagerViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, _gameService, storage, () => new CoverShuffleSettings(), new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _manager = new CoverShuffleManager(_repository, _coverService, _gameService);
            _bulkService = new BulkConfigurationService(_coverService, new FakeCoverShuffleLogger());
            _importExportService = new ImportExportService(_repository, storage, new FakeCoverShuffleLogger());
            _viewModel = new CoverShuffleManagerViewModel(_manager, _bulkService, _importExportService, _dialogs);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void IsEmpty_WhenNoGamesManaged_IsTrue()
        {
            Assert.True(_viewModel.IsEmpty);
            Assert.Empty(_viewModel.Games);
        }

        [Fact]
        public void SearchText_FiltersGamesByName()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Cyberpunk 2077");
            _gameService.SeedGameName(gameB, "Elden Ring");
            _coverService.EnableCoverShuffle(gameA);
            _coverService.EnableCoverShuffle(gameB);
            _viewModel.Reload();

            _viewModel.SearchText = "cyber";

            var row = Assert.Single(_viewModel.Games);
            Assert.Equal("Cyberpunk 2077", row.GameName);
        }

        [Fact]
        public void EnableSelected_EnablesOnlyCheckedGames()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Game A");
            _gameService.SeedGameName(gameB, "Game B");
            AddStoredCoverFor(gameA);
            AddStoredCoverFor(gameB);
            _viewModel.Reload();
            _viewModel.Games.Single(g => g.GameId == gameA).IsSelected = true;

            _viewModel.EnableSelected();

            Assert.True(_coverService.IsEnabled(gameA));
            Assert.False(_coverService.IsEnabled(gameB));
        }

        [Fact]
        public void EnableSelected_WithNoneSelected_SetsAGuidanceStatusMessage()
        {
            _viewModel.EnableSelected();

            Assert.Contains("Select", _viewModel.StatusMessage);
        }

        [Fact]
        public void Export_WithNoFolderSelected_DoesNothing()
        {
            _dialogs.NextSelectedFolder = null;

            _viewModel.Export();

            Assert.Null(_viewModel.StatusMessage);
        }

        [Fact]
        public void Export_WritesToTheSelectedFolder()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCoverFor(gameId);
            var exportFolder = Path.Combine(_tempDirectory, "export");
            _dialogs.NextSelectedFolder = exportFolder;

            _viewModel.Export();

            Assert.True(File.Exists(Path.Combine(exportFolder, "CoverShuffle.json")));
        }

        private void AddStoredCoverFor(Guid gameId)
        {
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            var storage = new CoverStorage(layout);
            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            var coverId = Guid.NewGuid();
            var relativePath = storage.SaveCoverFile(gameId, coverId, sourceFile);
            _repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            });
        }
    }
}
