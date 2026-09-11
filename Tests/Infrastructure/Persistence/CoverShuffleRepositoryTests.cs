using System;
using System.Collections.Generic;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
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

            repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId, SettingsOverride = new GameSettingsOverride { Enabled = true } });

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
                SettingsOverride = new GameSettingsOverride { Enabled = true }
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
            repository.SaveGameConfiguration(new GameConfiguration { GameId = firstGameId, SettingsOverride = new GameSettingsOverride { Enabled = true } });
            repository.SaveGameConfiguration(new GameConfiguration { GameId = secondGameId, SettingsOverride = new GameSettingsOverride { Enabled = false } });

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
        public void GameConfiguration_WithPartialOverride_PreservesUnsetFieldsAsNullAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.SaveGameConfiguration(new GameConfiguration
            {
                GameId = gameId,
                SettingsOverride = new GameSettingsOverride { Interval = TimeSpan.FromHours(6) }
            });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            var loaded = reloaded.GetGameConfiguration(gameId);

            Assert.Equal(TimeSpan.FromHours(6), loaded.SettingsOverride.Interval);
            Assert.Null(loaded.SettingsOverride.Enabled);
            Assert.Null(loaded.SettingsOverride.Mode);
            Assert.Null(loaded.SettingsOverride.NotificationPreference);
        }

        [Fact]
        public void Load_WithVersion1FullSnapshotOverride_MigratesToEquivalentExplicitOverrides()
        {
            Directory.CreateDirectory(_tempDirectory);
            var gameId = Guid.NewGuid();

            // Shape produced by the pre-migration code: every field in
            // SettingsOverride has an explicit value (cloned from global at
            // the time), plus the now-removed SteamGridDbApiKey.
            var version1Json = @"{
  ""SchemaVersion"": 1,
  ""GameConfigurations"": [
    {
      ""GameId"": """ + gameId + @""",
      ""SettingsOverride"": {
        ""Enabled"": true,
        ""Interval"": ""02:00:00"",
        ""Mode"": ""Interval"",
        ""AvoidConsecutiveDuplicates"": true,
        ""ShuffleOnStartup"": true,
        ""ShuffleOnGameLaunch"": false,
        ""NotificationPreference"": ""NotifyOnShuffle"",
        ""SteamGridDbApiKey"": ""should-be-ignored"",
        ""NewGameBehavior"": ""DoNothing""
      }
    }
  ],
  ""Covers"": [],
  ""ShuffleStates"": [],
  ""OriginalArtworkRecords"": []
}";
            File.WriteAllText(_databaseFilePath, version1Json);

            var repository = new CoverShuffleRepository(_databaseFilePath);
            var loaded = repository.GetGameConfiguration(gameId);

            Assert.NotNull(loaded);
            Assert.True(loaded.SettingsOverride.Enabled);
            Assert.Equal(TimeSpan.FromHours(2), loaded.SettingsOverride.Interval);
            Assert.Equal(ShuffleMode.Interval, loaded.SettingsOverride.Mode);
            Assert.True(loaded.SettingsOverride.AvoidConsecutiveDuplicates);
            Assert.True(loaded.SettingsOverride.ShuffleOnStartup);
            Assert.False(loaded.SettingsOverride.ShuffleOnGameLaunch);
            Assert.Equal(NotificationPreference.NotifyOnShuffle, loaded.SettingsOverride.NotificationPreference);
            Assert.Equal(NewGameBehavior.DoNothing, loaded.SettingsOverride.NewGameBehavior);
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
        public void ShuffleState_LastShuffleTrigger_RoundTripsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                CurrentCoverId = Guid.NewGuid(),
                LastShuffleTrigger = ShuffleTrigger.Manual
            });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            var state = reloaded.GetShuffleState(gameId);

            Assert.Equal(ShuffleTrigger.Manual, state.LastShuffleTrigger);
        }

        [Fact]
        public void Load_WithVersion2ShuffleStateMissingLastShuffleTrigger_DefaultsToRandom()
        {
            Directory.CreateDirectory(_tempDirectory);
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();

            // Shape produced before LastShuffleTrigger existed: no such
            // property on the ShuffleState record at all.
            var version2Json = @"{
  ""SchemaVersion"": 2,
  ""GameConfigurations"": [],
  ""Covers"": [],
  ""ShuffleStates"": [
    {
      ""GameId"": """ + gameId + @""",
      ""CurrentCoverId"": """ + coverId + @""",
      ""ShuffleCycle"": []
    }
  ],
  ""OriginalArtworkRecords"": []
}";
            File.WriteAllText(_databaseFilePath, version2Json);

            var repository = new CoverShuffleRepository(_databaseFilePath);
            var loaded = repository.GetShuffleState(gameId);

            Assert.NotNull(loaded);
            Assert.Equal(ShuffleTrigger.Random, loaded.LastShuffleTrigger);
        }

        [Fact]
        public void Load_WithVersion3CoverMissingIsFavorite_DefaultsToFalse()
        {
            Directory.CreateDirectory(_tempDirectory);
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();

            // Shape produced before IsFavorite existed on Cover: no such
            // property on the record at all.
            var version3Json = @"{
  ""SchemaVersion"": 3,
  ""GameConfigurations"": [],
  ""Covers"": [
    {
      ""CoverId"": """ + coverId + @""",
      ""GameId"": """ + gameId + @""",
      ""Source"": ""LocalFile"",
      ""LocalPath"": ""cover.png"",
      ""Hash"": ""abc"",
      ""IsEnabled"": true
    }
  ],
  ""ShuffleStates"": [],
  ""OriginalArtworkRecords"": []
}";
            File.WriteAllText(_databaseFilePath, version3Json);

            var repository = new CoverShuffleRepository(_databaseFilePath);
            var loaded = repository.GetCover(gameId, coverId);

            Assert.NotNull(loaded);
            Assert.False(loaded.IsFavorite);
        }

        [Fact]
        public void Cover_IsFavorite_RoundTripsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.AddCover(new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = "cover.png",
                Hash = "abc",
                IsEnabled = true,
                IsFavorite = true
            });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            var loaded = reloaded.GetCover(gameId, coverId);

            Assert.NotNull(loaded);
            Assert.True(loaded.IsFavorite);
        }

        [Fact]
        public void GetShuffleState_MutatingReturnedShuffleCycle_DoesNotAffectStoredState()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                ShuffleCycle = new List<Guid> { Guid.NewGuid() }
            });

            var firstRead = repository.GetShuffleState(gameId);
            firstRead.ShuffleCycle.Add(Guid.NewGuid());
            firstRead.ShuffleCycle.Clear();

            var secondRead = repository.GetShuffleState(gameId);
            Assert.Single(secondRead.ShuffleCycle);
        }

        [Fact]
        public void GetGameConfiguration_MutatingReturnedSettingsOverride_DoesNotAffectStoredConfiguration()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            repository.SaveGameConfiguration(new GameConfiguration
            {
                GameId = gameId,
                SettingsOverride = new GameSettingsOverride { Enabled = true }
            });

            var firstRead = repository.GetGameConfiguration(gameId);
            firstRead.SettingsOverride.Enabled = false;

            var secondRead = repository.GetGameConfiguration(gameId);
            Assert.True(secondRead.SettingsOverride.Enabled);
        }

        [Fact]
        public void ExecuteBatch_AppliesAllMutations_AndPersistsAcrossReload()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);
            var cover = NewCover(gameId);

            repository.ExecuteBatch(() =>
            {
                repository.AddCover(cover);
                repository.SaveShuffleState(new ShuffleState { GameId = gameId, CurrentCoverId = cover.CoverId });
                repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId });
            });

            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            Assert.Single(reloaded.GetCovers(gameId));
            Assert.Equal(cover.CoverId, reloaded.GetShuffleState(gameId).CurrentCoverId);
            Assert.NotNull(reloaded.GetGameConfiguration(gameId));
        }

        [Fact]
        public void ExecuteBatch_CoalescesMultipleMutationsIntoOnePersist()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            repository.AddCover(NewCover(gameId));
            repository.SaveShuffleState(new ShuffleState { GameId = gameId });
            repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId });
            Assert.Equal(3, repository.PersistCallCount);

            var batched = new CoverShuffleRepository(_databaseFilePath + ".batched");
            batched.ExecuteBatch(() =>
            {
                batched.AddCover(NewCover(gameId));
                batched.SaveShuffleState(new ShuffleState { GameId = gameId });
                batched.SaveGameConfiguration(new GameConfiguration { GameId = gameId });
            });

            Assert.Equal(1, batched.PersistCallCount);
        }

        [Fact]
        public void ExecuteBatch_Nested_OnlyPersistsOnceAtOutermostLevel()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            repository.ExecuteBatch(() =>
            {
                repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId });
                repository.ExecuteBatch(() =>
                {
                    repository.AddCover(NewCover(gameId));
                    repository.SaveShuffleState(new ShuffleState { GameId = gameId });
                });
            });

            Assert.Equal(1, repository.PersistCallCount);
        }

        [Fact]
        public void ExecuteBatch_WhenMutationsThrow_StillPersistsWhatWasAppliedAndPropagates()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            Assert.Throws<InvalidOperationException>(() =>
            {
                repository.ExecuteBatch(() =>
                {
                    repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId });
                    throw new InvalidOperationException("boom");
                });
            });

            Assert.Equal(1, repository.PersistCallCount);
            Assert.NotNull(repository.GetGameConfiguration(gameId));
        }

        [Fact]
        public void ExecuteBatch_WithNullMutations_ThrowsArgumentNullException()
        {
            var repository = new CoverShuffleRepository(_databaseFilePath);
            Assert.Throws<ArgumentNullException>(() => repository.ExecuteBatch(null));
        }

        [Fact]
        public void Persist_CalledTwice_UsesReplaceOnSecondWriteAndKeepsDataValid()
        {
            var gameId = Guid.NewGuid();
            var repository = new CoverShuffleRepository(_databaseFilePath);

            // First mutation: no destination file exists yet, so Persist()
            // takes the File.Move branch.
            repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId, SettingsOverride = new GameSettingsOverride { Enabled = true } });
            Assert.True(File.Exists(_databaseFilePath));

            // Second mutation: the destination now exists, so Persist() must
            // take the crash-safe File.Replace branch instead.
            var cover = NewCover(gameId);
            repository.AddCover(cover);

            Assert.False(File.Exists(_databaseFilePath + ".tmp"));
            var reloaded = new CoverShuffleRepository(_databaseFilePath);
            Assert.NotNull(reloaded.GetGameConfiguration(gameId));
            Assert.Single(reloaded.GetCovers(gameId));
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
