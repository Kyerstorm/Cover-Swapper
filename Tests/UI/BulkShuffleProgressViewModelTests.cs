using System;
using System.IO;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class BulkShuffleProgressViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly PlayniteCoverService _coverService;
        private readonly CoverShuffleManager _manager;
        private readonly BulkShuffleService _service;

        public BulkShuffleProgressViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "BulkShuffleProgressViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => new CoverShuffleSettings { Enabled = true }, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _manager = new CoverShuffleManager(_repository, _coverService, _gameService, _storage);
            _service = new BulkShuffleService(_manager, _coverService, _gameService, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private Guid CreateEligibleGame()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);

            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            var coverId = Guid.NewGuid();
            var relativePath = _storage.SaveCoverFile(gameId, coverId, sourceFile);
            _repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                IsEnabled = true
            });

            return gameId;
        }

        [Fact]
        public async Task RunAsync_OnCompletion_ExposesTheResultAndStopsRunning()
        {
            var gameId = CreateEligibleGame();
            var viewModel = new BulkShuffleProgressViewModel(_service, new[] { gameId }, "Shuffling selected games...");

            await viewModel.RunAsync();

            Assert.False(viewModel.IsRunning);
            Assert.True(viewModel.IsComplete);
            Assert.Equal(1, viewModel.Result.Shuffled);
            Assert.Contains("Shuffle complete", viewModel.SummaryText);
            Assert.Contains("1 game(s) shuffled", viewModel.SummaryText);
        }

        [Fact]
        public async Task RunAsync_WithNullGameIds_ShufflesEveryInstalledManagedGame()
        {
            var gameId = CreateEligibleGame();
            var viewModel = new BulkShuffleProgressViewModel(_service, null, "Shuffling installed games...");

            await viewModel.RunAsync();

            Assert.Equal(1, viewModel.Result.Total);
            Assert.Equal(1, viewModel.Result.Shuffled);
            Assert.NotNull(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public async Task RunAsync_WithFailures_SummaryMentionsThem_AndDetailsListsGameNames()
        {
            var throwingGameId = Guid.NewGuid();
            _gameService.GameIdToThrowOn = throwingGameId;
            _gameService.SeedGameName(throwingGameId, "Broken Game");
            _gameService.SeedInstalled(throwingGameId, true);
            _coverService.EnableCoverShuffle(throwingGameId);
            var sourceFile = Path.Combine(_tempDirectory, "cover.png");
            File.WriteAllBytes(sourceFile, new byte[] { 1 });
            var coverId = Guid.NewGuid();
            _repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = throwingGameId,
                Source = CoverSource.LocalFile,
                LocalPath = _storage.SaveCoverFile(throwingGameId, coverId, sourceFile),
                Hash = "hash",
                IsEnabled = true
            });

            var viewModel = new BulkShuffleProgressViewModel(_service, new[] { throwingGameId }, "Shuffling selected games...");

            await viewModel.RunAsync();

            Assert.True(viewModel.HasFailures);
            Assert.Contains("1 failed", viewModel.SummaryText);
            Assert.Contains("Broken Game", viewModel.DetailsText);
        }

        [Fact]
        public async Task RunAsync_WithNoFailures_HasFailuresIsFalse()
        {
            var gameId = CreateEligibleGame();
            var viewModel = new BulkShuffleProgressViewModel(_service, new[] { gameId }, "Shuffling selected games...");

            await viewModel.RunAsync();

            Assert.False(viewModel.HasFailures);
        }

        [Fact]
        public void ShowDetails_DefaultsToFalse()
        {
            var viewModel = new BulkShuffleProgressViewModel(_service, new Guid[0], "Shuffling selected games...");

            Assert.False(viewModel.ShowDetails);
        }

        [Fact]
        public async Task RunAsync_BeforeCompletion_IsRunningIsTrue()
        {
            var viewModel = new BulkShuffleProgressViewModel(_service, new Guid[0], "Shuffling selected games...");

            Assert.True(viewModel.IsRunning);
            Assert.False(viewModel.IsComplete);

            await viewModel.RunAsync();

            Assert.False(viewModel.IsRunning);
        }

        [Fact]
        public void Cancel_BeforeRunning_DoesNotThrow()
        {
            var viewModel = new BulkShuffleProgressViewModel(_service, new Guid[0], "Shuffling selected games...");

            var exception = Record.Exception(() => viewModel.Cancel());

            Assert.Null(exception);
        }
    }
}
