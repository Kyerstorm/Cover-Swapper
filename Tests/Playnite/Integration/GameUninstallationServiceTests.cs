using System;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class GameUninstallationServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverStorageLayout _layout;
        private readonly FakePlayniteGameService _gameService = new FakePlayniteGameService();
        private readonly FakeCoverShuffleLogger _logger = new FakeCoverShuffleLogger();
        private readonly GameUninstallationService _service;

        public GameUninstallationServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "GameUninstallationServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            _layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            _layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(_layout);
            _service = new GameUninstallationService(_repository, _storage, _gameService, _logger);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

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

        private void SetUpFullyConfiguredGame(Guid gameId, string originalCoverReference, int coverCount)
        {
            _gameService.SeedCoverReference(gameId, originalCoverReference);
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo
            {
                GameId = gameId,
                OriginalCoverReference = originalCoverReference,
                CapturedAtUtc = DateTime.UtcNow
            });
            _repository.SaveGameConfiguration(new GameConfiguration
            {
                GameId = gameId,
                SettingsOverride = new GameSettingsOverride { Enabled = true }
            });

            for (var i = 0; i < coverCount; i++)
            {
                AddStoredCover(gameId);
            }

            _repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                CurrentCoverId = _repository.GetCovers(gameId).FirstOrDefault()?.CoverId,
                LastShuffleAt = DateTime.UtcNow,
                NextShuffleAt = DateTime.UtcNow.AddHours(24)
            });

            // Simulate Cover Shuffle currently displaying one of its own
            // covers, as it would after at least one shuffle.
            _gameService.SetCoverReference(gameId, "some-shuffled-cover.png");
        }

        // --- Successful uninstall -------------------------------------------------

        [Fact]
        public void HandleGameUninstalled_RestoresTheOriginalCover()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 3);

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.True(result.Success);
            Assert.True(result.OriginalRestored);
            Assert.Equal("original-cover.png", _gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_DeletesEveryCoverFile()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 3);
            var coverFiles = _repository.GetCovers(gameId).Select(c => _storage.GetAbsolutePath(c.LocalPath)).ToList();

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Equal(3, result.FilesDeleted);
            Assert.All(coverFiles, f => Assert.False(File.Exists(f)));
        }

        [Fact]
        public void HandleGameUninstalled_RemovesEveryCoverRecord()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 6);

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Equal(6, result.CoverCount);
            Assert.True(result.RecordsRemoved);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_RemovesShuffleState()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.True(result.ShuffleStateRemoved);
            Assert.Null(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_RemovesOriginalArtworkRecord()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Null(_repository.GetOriginalArtwork(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_RemovesGameConfiguration()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Null(_repository.GetGameConfiguration(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_LogsASummary()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 6);

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Contains(_logger.InfoMessages, m => m.Contains("Fallout 4") && m.Contains("Covers: 6") && m.Contains("Files deleted: 6"));
        }

        // --- No original cover ------------------------------------------------

        [Fact]
        public void HandleGameUninstalled_WithNoOriginalCoverCaptured_ClearsThePlayniteCover()
        {
            var gameId = Guid.NewGuid();
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo { GameId = gameId, OriginalCoverReference = null, CapturedAtUtc = DateTime.UtcNow });
            AddStoredCover(gameId);
            _gameService.SetCoverReference(gameId, "some-cover-shuffle-cover.png");

            var result = _service.HandleGameUninstalled(gameId, "New Game");

            Assert.True(result.Success);
            Assert.Null(_gameService.GetCoverReference(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_WithNoOriginalCoverCaptured_StillRemovesPluginAssets()
        {
            var gameId = Guid.NewGuid();
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo { GameId = gameId, OriginalCoverReference = null, CapturedAtUtc = DateTime.UtcNow });
            var cover = AddStoredCover(gameId);

            _service.HandleGameUninstalled(gameId, "New Game");

            Assert.Empty(_repository.GetCovers(gameId));
            Assert.False(File.Exists(_storage.GetAbsolutePath(cover.LocalPath)));
            Assert.Null(_repository.GetOriginalArtwork(gameId));
        }

        // --- No Cover Shuffle involvement at all -------------------------------

        [Fact]
        public void HandleGameUninstalled_ForAGameCoverShuffleNeverTouched_ReportsNothingToDo()
        {
            var gameId = Guid.NewGuid();

            var result = _service.HandleGameUninstalled(gameId, "Untouched Game");

            Assert.True(result.Success);
            Assert.True(result.NothingToDo);
        }

        // --- Restoration failure ------------------------------------------------

        [Fact]
        public void HandleGameUninstalled_WhenRestorationThrows_LeavesCoversAndRecordsInPlace()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 3);
            _gameService.GameIdToThrowOn = gameId;
            var coverFiles = _repository.GetCovers(gameId).Select(c => _storage.GetAbsolutePath(c.LocalPath)).ToList();

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.False(result.Success);
            Assert.Equal(3, _repository.GetCovers(gameId).Count);
            Assert.NotNull(_repository.GetShuffleState(gameId));
            Assert.NotNull(_repository.GetOriginalArtwork(gameId));
            Assert.NotNull(_repository.GetGameConfiguration(gameId));
            Assert.All(coverFiles, f => Assert.True(File.Exists(f)));
        }

        [Fact]
        public void HandleGameUninstalled_WhenRestorationCannotBeVerified_LeavesEverythingInPlace_WithoutThrowing()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 2);
            // Simulates Playnite silently failing to apply the change (e.g.
            // the game could no longer be resolved) - no exception, but the
            // cover never actually changes.
            _gameService.GameIdsIgnoringSetCoverReference.Add(gameId);

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.False(result.Success);
            Assert.Equal(2, _repository.GetCovers(gameId).Count);
            Assert.NotNull(_repository.GetShuffleState(gameId));
        }

        [Fact]
        public void HandleGameUninstalled_WhenRestorationFails_LogsAnError()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);
            _gameService.GameIdToThrowOn = gameId;

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Contains(_logger.ErrorMessages, m => m.Contains("Fallout 4") || m.Contains(gameId.ToString()));
        }

        [Fact]
        public void HandleGameUninstalled_WhenRestorationFails_ReturnsTheExpectedUserFacingMessage()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);
            _gameService.GameIdsIgnoringSetCoverReference.Add(gameId);

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.Contains("could not restore the original artwork for Fallout 4", result.ErrorMessage);
            Assert.Contains("No Cover Shuffle files were deleted", result.ErrorMessage);
        }

        [Fact]
        public void HandleGameUninstalled_WhenRestorationFails_DoesNotDeleteAnyCoverFiles()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 2);
            _gameService.GameIdsIgnoringSetCoverReference.Add(gameId);
            var coverFiles = _repository.GetCovers(gameId).Select(c => _storage.GetAbsolutePath(c.LocalPath)).ToList();

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.All(coverFiles, f => Assert.True(File.Exists(f)));
        }

        // --- Partial file deletion ----------------------------------------------

        [Fact]
        public void HandleGameUninstalled_WhenOneCoverFileIsLocked_DeletesTheOthersAndReportsTheFailure()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);
            var lockedCover = AddStoredCover(gameId);
            var unlockedCover = AddStoredCover(gameId);
            var lockedPath = _storage.GetAbsolutePath(lockedCover.LocalPath);

            using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

                Assert.True(result.Success);
                Assert.Equal(1, result.FilesFailedToDelete);
                Assert.Equal(2, result.FilesDeleted);
                Assert.True(File.Exists(lockedPath));
                Assert.False(File.Exists(_storage.GetAbsolutePath(unlockedCover.LocalPath)));
            }
        }

        [Fact]
        public void HandleGameUninstalled_WhenAFileFailsToDelete_StillRemovesTheLogicalRecordsSafely()
        {
            var gameId = Guid.NewGuid();
            var lockedCover = AddStoredCover(gameId);
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo { GameId = gameId, OriginalCoverReference = "original.png" });
            _gameService.SeedCoverReference(gameId, "original.png");
            var lockedPath = _storage.GetAbsolutePath(lockedCover.LocalPath);

            using (new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

                Assert.True(result.Success);
                Assert.Equal(1, result.FilesFailedToDelete);
                // The physical file survives, but the logical record is
                // still removed - a deletion failure is reported, not
                // treated as a reason to leave stale bookkeeping behind.
                Assert.Empty(_repository.GetCovers(gameId));
            }
        }

        [Fact]
        public void HandleGameUninstalled_ForMissingCoverFile_TreatsItAsAlreadyDeleted()
        {
            var gameId = Guid.NewGuid();
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo { GameId = gameId, OriginalCoverReference = "original.png" });
            _gameService.SeedCoverReference(gameId, "original.png");
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var result = _service.HandleGameUninstalled(gameId, "Fallout 4");

            Assert.True(result.Success);
            Assert.Equal(1, result.FilesDeleted);
            Assert.Equal(0, result.FilesFailedToDelete);
        }

        // --- Reinstall ------------------------------------------------------------

        [Fact]
        public void HandleGameUninstalled_AfterSuccess_LooksLikeANeverConfiguredGame_SoReinstallIsTreatedAsNew()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 2);

            _service.HandleGameUninstalled(gameId, "Fallout 4");

            // GameInstallationService.IsAlreadyKnown treats a game as new
            // whenever both of these are null - exactly what a genuinely
            // fresh install looks like, and exactly what this cleanup must
            // restore the game to.
            Assert.Null(_repository.GetGameConfiguration(gameId));
            Assert.Null(_repository.GetOriginalArtwork(gameId));
        }

        // --- Unrelated game ---------------------------------------------------

        [Fact]
        public void HandleGameUninstalled_DoesNotAffectAnUnrelatedGamesCoversSettingsOrState()
        {
            var uninstalledGameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(uninstalledGameId, "original-cover.png", coverCount: 2);

            var otherGameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(otherGameId, "other-original.png", coverCount: 3);

            _service.HandleGameUninstalled(uninstalledGameId, "Fallout 4");

            Assert.Equal(3, _repository.GetCovers(otherGameId).Count);
            Assert.NotNull(_repository.GetGameConfiguration(otherGameId));
            Assert.NotNull(_repository.GetOriginalArtwork(otherGameId));
            Assert.NotNull(_repository.GetShuffleState(otherGameId));
            Assert.Equal("other-original.png", _repository.GetOriginalArtwork(otherGameId).OriginalCoverReference);
        }

        [Fact]
        public void HandleGameUninstalled_UnexpectedException_IsCaught_AndReturnsAFailureResult()
        {
            var gameId = Guid.NewGuid();
            SetUpFullyConfiguredGame(gameId, "original-cover.png", coverCount: 1);
            _gameService.GameIdToThrowOn = gameId;

            var exception = Record.Exception(() => _service.HandleGameUninstalled(gameId, "Fallout 4"));

            Assert.Null(exception);
        }
    }
}
