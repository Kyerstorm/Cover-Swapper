using System;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class BulkShuffleServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly PlayniteCoverService _coverService;
        private readonly CoverShuffleManager _manager;
        private readonly BulkShuffleService _service;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings { Enabled = true };

        public BulkShuffleServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "BulkShuffleServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => _globalSettings, new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
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

        private Cover AddStoredCover(Guid gameId, bool enabled = true)
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
                IsEnabled = enabled
            };
            _repository.AddCover(cover);
            return cover;
        }

        private Guid CreateEligibleGame(string name = "Some Game")
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, name);
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);
            AddStoredCover(gameId);
            return gameId;
        }

        [Fact]
        public void ShuffleGames_WithEmptyList_ReturnsZeroedResult()
        {
            var result = _service.ShuffleGames(new Guid[0]);

            Assert.Equal(0, result.Total);
            Assert.Equal(0, result.Shuffled);
            Assert.Empty(result.Failures);
        }

        [Fact]
        public void ShuffleGames_WithNullList_DoesNotThrow()
        {
            var result = _service.ShuffleGames(null);

            Assert.Equal(0, result.Total);
        }

        [Fact]
        public void ShuffleGames_EligibleGame_ShufflesItAndUpdatesState()
        {
            var gameId = CreateEligibleGame();

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Shuffled);
            Assert.NotNull(_repository.GetShuffleState(gameId));
            Assert.NotNull(_repository.GetShuffleState(gameId).CurrentCoverId);
        }

        [Fact]
        public void ShuffleGames_UninstalledGame_IsSkippedAsNotInstalled()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, false);
            _coverService.EnableCoverShuffle(gameId);
            AddStoredCover(gameId);

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.SkippedNotInstalled);
            Assert.Equal(0, result.Shuffled);
            Assert.Null(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public void ShuffleGames_DisabledGame_IsSkippedAsDisabled()
        {
            _globalSettings = new CoverShuffleSettings { Enabled = false };
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            AddStoredCover(gameId);

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.SkippedDisabled);
            Assert.Equal(0, result.Shuffled);
        }

        [Fact]
        public void ShuffleGames_GameWithNoCovers_IsSkippedAsNoCovers()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.SkippedNoCovers);
            Assert.Equal(0, result.Shuffled);
        }

        [Fact]
        public void ShuffleGames_GameWithOnlyMissingCoverFiles_IsSkippedAsNoCovers()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.SkippedNoCovers);
            Assert.Equal(0, result.Shuffled);
        }

        [Fact]
        public void ShuffleGames_GameWithOnlyDisabledCovers_IsSkippedAsNoCovers()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);
            AddStoredCover(gameId, enabled: false);

            var result = _service.ShuffleGames(new[] { gameId });

            Assert.Equal(1, result.SkippedNoCovers);
        }

        [Fact]
        public void ShuffleGames_MultipleGames_CountsEachIndependently()
        {
            var shuffled = CreateEligibleGame("Shuffled Game");

            var notInstalled = Guid.NewGuid();
            _gameService.SeedInstalled(notInstalled, false);
            _coverService.EnableCoverShuffle(notInstalled);
            AddStoredCover(notInstalled);

            var disabled = Guid.NewGuid();
            _gameService.SeedInstalled(disabled, true);
            _coverService.DisableCoverShuffle(disabled);
            AddStoredCover(disabled);

            var noCovers = Guid.NewGuid();
            _gameService.SeedInstalled(noCovers, true);
            _coverService.EnableCoverShuffle(noCovers);

            var result = _service.ShuffleGames(new[] { shuffled, notInstalled, disabled, noCovers });

            Assert.Equal(4, result.Total);
            Assert.Equal(1, result.Shuffled);
            Assert.Equal(1, result.SkippedNotInstalled);
            Assert.Equal(1, result.SkippedDisabled);
            Assert.Equal(1, result.SkippedNoCovers);
            Assert.Equal(0, result.Failed);
        }

        [Fact]
        public void ShuffleGames_UnexpectedException_IsRecordedAsAFailure_WithoutStoppingTheBatch()
        {
            var throwingGameId = Guid.NewGuid();
            _gameService.GameIdToThrowOn = throwingGameId;
            _gameService.SeedInstalled(throwingGameId, true);
            _coverService.EnableCoverShuffle(throwingGameId);
            AddStoredCover(throwingGameId);
            _gameService.SeedGameName(throwingGameId, "Broken Game");

            var succeeding = CreateEligibleGame("Fine Game");

            var result = _service.ShuffleGames(new[] { throwingGameId, succeeding });

            Assert.Equal(1, result.Failed);
            Assert.Equal(1, result.Shuffled);
            Assert.Single(result.Failures);
            Assert.Equal("Broken Game", result.Failures[0].GameName);
            Assert.Equal(throwingGameId, result.Failures[0].GameId);
        }

        [Fact]
        public void ShuffleGames_ReportsProgressAfterEachGame()
        {
            var first = CreateEligibleGame("First");
            var second = CreateEligibleGame("Second");
            var reports = new System.Collections.Generic.List<BulkShuffleProgress>();
            var progress = new SynchronousProgress<BulkShuffleProgress>(p => reports.Add(p));

            _service.ShuffleGames(new[] { first, second }, progress);

            Assert.Equal(2, reports.Count);
            Assert.Equal(1, reports[0].Completed);
            Assert.Equal(2, reports[0].Total);
            Assert.Equal(2, reports[1].Completed);
        }

        [Fact]
        public void ShuffleInstalledGames_OnlyShufflesGamesCoverShuffleManages()
        {
            var managed = CreateEligibleGame("Managed Game");
            var unmanaged = Guid.NewGuid();
            _gameService.SeedInstalled(unmanaged, true);

            var result = _service.ShuffleInstalledGames();

            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Shuffled);
            Assert.NotNull(_repository.GetShuffleState(managed));
            Assert.Null(_repository.GetShuffleState(unmanaged));
        }

        [Fact]
        public void ShuffleInstalledGames_WithNoManagedGames_ReturnsEmptyResult()
        {
            var result = _service.ShuffleInstalledGames();

            Assert.Equal(0, result.Total);
            Assert.Equal(0, result.Shuffled);
        }

        [Fact]
        public void ShuffleGames_DoesNotStartANewRandomizedCycle_ItReusesTheSharedShuffleEngine()
        {
            // FakeShuffleRandomizer is deterministic (leaves cycle order
            // untouched), matching PlayniteCoverServiceTests' own
            // shuffle-engine tests - proving this service produces the exact
            // same selection ShuffleToNextCover would, rather than
            // implementing a second randomization algorithm.
            var gameId = Guid.NewGuid();
            _gameService.SeedInstalled(gameId, true);
            _coverService.EnableCoverShuffle(gameId);
            var expected = AddStoredCover(gameId);

            _service.ShuffleGames(new[] { gameId });

            Assert.Equal(expected.CoverId, _repository.GetShuffleState(gameId).CurrentCoverId);
        }
    }

    /// <summary>
    /// A synchronous <see cref="IProgress{T}"/> for tests: the real
    /// <see cref="Progress{T}"/> marshals callbacks through the captured
    /// <see cref="System.Threading.SynchronizationContext"/> asynchronously,
    /// which would make progress-reporting assertions flaky/order-dependent
    /// in a unit test.
    /// </summary>
    internal class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value) => _handler(value);
    }
}
