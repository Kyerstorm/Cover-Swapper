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
    public class CoverShuffleManagerTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly PlayniteCoverService _coverService;
        private readonly CoverShuffleManager _manager;

        public CoverShuffleManagerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleManagerTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, _gameService, _storage, () => new CoverShuffleSettings(), new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _manager = new CoverShuffleManager(_repository, _coverService, _gameService, _storage);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private void AddStoredCover(Guid gameId)
        {
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
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            });
        }

        [Fact]
        public void GetManagedGames_IncludesGamesWithOnlyCovers_EvenIfNeverEnabled()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            AddStoredCover(gameId);

            var games = _manager.GetManagedGames();

            var row = Assert.Single(games);
            Assert.Equal("Some Game", row.GameName);
            Assert.Equal(2, row.CoverCount);
            Assert.False(row.IsEnabled);
        }

        [Fact]
        public void GetManagedGames_IncludesEnabledGamesWithNoCoversYet()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Empty Game");
            _coverService.EnableCoverShuffle(gameId);

            var games = _manager.GetManagedGames();

            var row = Assert.Single(games);
            Assert.Equal(0, row.CoverCount);
            Assert.True(row.IsEnabled);
        }

        [Fact]
        public void GetManagedGames_IsOrderedByGameName()
        {
            var gameA = Guid.NewGuid();
            var gameB = Guid.NewGuid();
            _gameService.SeedGameName(gameA, "Zelda");
            _gameService.SeedGameName(gameB, "Astro Bot");
            _coverService.EnableCoverShuffle(gameA);
            _coverService.EnableCoverShuffle(gameB);

            var games = _manager.GetManagedGames();

            Assert.Equal("Astro Bot", games[0].GameName);
            Assert.Equal("Zelda", games[1].GameName);
        }

        [Fact]
        public void GetManagedGames_WhenNothingConfigured_ReturnsEmpty()
        {
            Assert.Empty(_manager.GetManagedGames());
        }

        [Fact]
        public void GetManagedGames_WhenACoverFileIsMissing_FlagsTheGame()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            var cover = _repository.GetCovers(gameId).Single();
            System.IO.File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.True(row.HasMissingCover);
        }

        [Fact]
        public void GetManagedGames_WhenAllCoverFilesPresent_DoesNotFlagTheGame()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.False(row.HasMissingCover);
        }

        [Fact]
        public void GetManagedGames_ReportsDistinctCoverSources()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.Contains(CoverSource.LocalFile, row.Sources);
        }

        [Fact]
        public void GetManagedGames_WhenACoverFileExistsButCannotBeDecoded_FlagsItAsCorrupt()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId); // AddStoredCover writes garbage bytes, not a real image.

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.False(row.HasMissingCover);
            Assert.True(row.HasCorruptCover);
        }

        [Fact]
        public void GetManagedGames_WhenCurrentCoverIdDoesNotMatchAnyStoredCover_FlagsAnInvalidReference()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            _repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                CurrentCoverId = Guid.NewGuid() // Not one of this game's covers.
            });

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.True(row.HasInvalidCoverReference);
        }

        [Fact]
        public void GetManagedGames_WhenCurrentCoverIdMatchesAStoredCover_DoesNotFlagAnInvalidReference()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            var cover = _repository.GetCovers(gameId).Single();
            _repository.SaveShuffleState(new ShuffleState { GameId = gameId, CurrentCoverId = cover.CoverId });

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.False(row.HasInvalidCoverReference);
        }

        [Fact]
        public void GetManagedGames_SurfacesThePersistedNextShuffleTime()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);
            var nextShuffle = DateTime.UtcNow.AddHours(3);
            _repository.SaveShuffleState(new ShuffleState { GameId = gameId, NextShuffleAt = nextShuffle });

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.Equal(nextShuffle, row.NextShuffleAt);
        }

        [Fact]
        public void GetManagedGames_WhenNoShuffleStateExists_LeavesNextShuffleAtNull()
        {
            var gameId = Guid.NewGuid();
            _gameService.SeedGameName(gameId, "Some Game");
            AddStoredCover(gameId);

            var row = Assert.Single(_manager.GetManagedGames());

            Assert.Null(row.NextShuffleAt);
        }
    }
}
