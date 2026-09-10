using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Persistence
{
    public class CoverShuffleRepositoryTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _databaseFilePath;

        public CoverShuffleRepositoryTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleTests_" + Guid.NewGuid().ToString("N"));
            _databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static Cover NewCover(Guid gameId) => new Cover
        {
            CoverId = Guid.NewGuid(),
            GameId = gameId,
            Source = CoverSource.LocalFile,
            AddedAt = DateTime.UtcNow
        };

        [Fact]
        public void Constructor_WithCorruptDatabaseFile_StartsFreshInsteadOfThrowing()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(_databaseFilePath, "{ this is not valid json");

            var repository = new CoverShuffleRepository(_databaseFilePath);

            Assert.Empty(repository.GetAllGameConfigurations());
        }

        [Fact]
        public void Constructor_WithCorruptDatabaseFile_PreservesItAsABackup()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(_databaseFilePath, "{ this is not valid json");

            new CoverShuffleRepository(_databaseFilePath);

            var backupFiles = Directory.GetFiles(_tempDirectory, "*.corrupt-*");
            Assert.Single(backupFiles);
        }

        [Fact]
        public void Constructor_AfterCorruptFileRecovery_CanStillPersistNewData()
        {
            Directory.CreateDirectory(_tempDirectory);
            File.WriteAllText(_databaseFilePath, "not json at all");
            var repository = new CoverShuffleRepository(_databaseFilePath);
            var gameId = Guid.NewGuid();

            repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId, SettingsOverride = new CoverShuffleSettings { Enabled = true } });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            Assert.NotNull(reloaded.GetGameConfiguration(gameId));
        }

        [Fact]
        public void AddCover_UpToLimit_Succeeds_And11thIsRejected()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                repository.AddCover(NewCover(gameId));
            }

            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, repository.GetCovers(gameId).Count);
            Assert.Throws<CoverLimitExceededException>(() => repository.AddCover(NewCover(gameId)));
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void AddCover_LimitIsPerGame_OtherGamesUnaffected()
        {
            var gameId = Guid.NewGuid();
            var otherGameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                repository.AddCover(NewCover(gameId));
            }

            repository.AddCover(NewCover(otherGameId));

            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, repository.GetCovers(gameId).Count);
            Assert.Single(repository.GetCovers(otherGameId));
        }

        [Fact]
        public void Covers_PersistAcrossRepositoryReload()
        {
            var gameId = Guid.NewGuid();

            var repository = new CoverShuffleRepository(_databaseFilePath);
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                repository.AddCover(NewCover(gameId));
            }

            // Simulates a Playnite restart: a fresh instance loading the same file.
            var reloaded = new CoverShuffleRepository(_databaseFilePath);

            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, reloaded.GetCovers(gameId).Count);
            Assert.Throws<CoverLimitExceededException>(() => reloaded.AddCover(NewCover(gameId)));
        }

        [Fact]
        public void RemoveCover_FreesUpSlotForANewCover()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            var covers = new Cover[CoverLimitPolicy.MaxCoversPerGame];
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                covers[i] = NewCover(gameId);
                repository.AddCover(covers[i]);
            }

            repository.RemoveCover(gameId, covers[0].CoverId);
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame - 1, repository.GetCovers(gameId).Count);

            repository.AddCover(NewCover(gameId));
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void GetCovers_ForUnknownGame_ReturnsEmpty()
        {
            var repository = new CoverShuffleRepository(_databaseFilePath);
            Assert.Empty(repository.GetCovers(Guid.NewGuid()));
        }

        [Fact]
        public void GameConfiguration_RoundTripsAndPersistsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var configuration = new GameConfiguration
            {
                GameId = gameId,
                SettingsOverride = new CoverShuffleSettings { Enabled = true }
            };

            var repository = new CoverShuffleRepository(_databaseFilePath);
            Assert.Null(repository.GetGameConfiguration(gameId));

            repository.SaveGameConfiguration(configuration);

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            var loaded = reloaded.GetGameConfiguration(gameId);

            Assert.NotNull(loaded);
            Assert.True(loaded.SettingsOverride.Enabled);
        }

        [Fact]
        public void GetAllGameConfigurations_ReturnsEveryGameEverSaved()
        {
            var repository = new CoverShuffleRepository(_databaseFilePath);
            var firstGameId = Guid.NewGuid();
            var secondGameId = Guid.NewGuid();
            repository.SaveGameConfiguration(new GameConfiguration { GameId = firstGameId, SettingsOverride = new CoverShuffleSettings { Enabled = true } });
            repository.SaveGameConfiguration(new GameConfiguration { GameId = secondGameId, SettingsOverride = new CoverShuffleSettings { Enabled = false } });

            var all = repository.GetAllGameConfigurations();

            Assert.Equal(2, all.Count);
            Assert.Contains(all, c => c.GameId == firstGameId);
            Assert.Contains(all, c => c.GameId == secondGameId);
        }

        [Fact]
        public void GetAllGameConfigurations_WhenNoneSaved_ReturnsEmpty()
        {
            var repository = new CoverShuffleRepository(_databaseFilePath);

            Assert.Empty(repository.GetAllGameConfigurations());
        }

        [Fact]
        public void ShuffleState_RoundTripsAndPersistsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                CurrentCoverId = coverId,
                LastShuffleAt = now
            });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            var state = reloaded.GetShuffleState(gameId);

            Assert.NotNull(state);
            Assert.Equal(coverId, state.CurrentCoverId);
        }

        [Fact]
        public void OriginalArtwork_SavedThenCleared_IsNoLongerRetrievable()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            repository.SaveOriginalArtwork(new OriginalArtworkInfo
            {
                GameId = gameId,
                OriginalCoverReference = "original-cover-ref",
                CapturedAtUtc = DateTime.UtcNow
            });

            Assert.NotNull(repository.GetOriginalArtwork(gameId));

            repository.ClearOriginalArtwork(gameId);

            Assert.Null(repository.GetOriginalArtwork(gameId));
        }
    }
}
