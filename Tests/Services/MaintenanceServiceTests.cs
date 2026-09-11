using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Services
{
    public class MaintenanceServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverStorageLayout _layout;
        private readonly MaintenanceService _service;

        public MaintenanceServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "MaintenanceServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            _layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            _layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(_layout);
            _service = new MaintenanceService(_repository, _storage, _layout, new FakeCoverShuffleLogger());
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

        [Fact]
        public void Scan_OnCleanState_ReturnsEmptyReport()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);

            var report = _service.Scan();

            Assert.True(report.IsEmpty);
        }

        [Fact]
        public void Scan_FindsOrphanedFile_NotReferencedByAnyCoverRecord()
        {
            var gameId = Guid.NewGuid();
            var orphanDirectory = Path.Combine(_layout.CoversPath, gameId.ToString("N"));
            Directory.CreateDirectory(orphanDirectory);
            var orphanPath = Path.Combine(orphanDirectory, "orphan.png");
            File.WriteAllBytes(orphanPath, new byte[] { 1 });

            var report = _service.Scan();

            Assert.Contains(orphanPath, report.OrphanedCoverFiles);
        }

        [Fact]
        public void Scan_FindsInvalidRecord_WhoseFileIsMissing()
        {
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));

            var report = _service.Scan();

            Assert.Contains(report.InvalidCoverRecords, c => c.CoverId == cover.CoverId);
        }

        [Fact]
        public void Scan_FindsCacheFiles()
        {
            File.WriteAllBytes(Path.Combine(_layout.CachePath, "cached.png"), new byte[] { 1 });

            var report = _service.Scan();

            Assert.Single(report.CacheFiles);
        }

        [Fact]
        public void DeleteOrphanedCoverFiles_RemovesTheFilesFromDisk()
        {
            var gameId = Guid.NewGuid();
            var orphanDirectory = Path.Combine(_layout.CoversPath, gameId.ToString("N"));
            Directory.CreateDirectory(orphanDirectory);
            var orphanPath = Path.Combine(orphanDirectory, "orphan.png");
            File.WriteAllBytes(orphanPath, new byte[] { 1 });
            var report = _service.Scan();

            _service.DeleteOrphanedCoverFiles(report.OrphanedCoverFiles);

            Assert.False(File.Exists(orphanPath));
        }

        [Fact]
        public void RemoveInvalidCoverRecords_RemovesFromRepository_WithoutThrowing()
        {
            var gameId = Guid.NewGuid();
            var cover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(cover.LocalPath));
            var report = _service.Scan();

            _service.RemoveInvalidCoverRecords(report.InvalidCoverRecords);

            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void ClearCache_RemovesAllCacheFiles()
        {
            File.WriteAllBytes(Path.Combine(_layout.CachePath, "cached.png"), new byte[] { 1 });

            _service.ClearCache();

            Assert.Empty(Directory.GetFiles(_layout.CachePath));
        }

        [Fact]
        public void DeleteCacheFiles_OnlyRemovesTheSelectedFiles()
        {
            var keepPath = Path.Combine(_layout.CachePath, "keep.png");
            var deletePath = Path.Combine(_layout.CachePath, "delete.png");
            File.WriteAllBytes(keepPath, new byte[] { 1 });
            File.WriteAllBytes(deletePath, new byte[] { 2 });

            _service.DeleteCacheFiles(new[] { deletePath });

            Assert.True(File.Exists(keepPath));
            Assert.False(File.Exists(deletePath));
        }

        [Fact]
        public void Scan_ComputesHealthSummaryCounts()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            var missingCover = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(missingCover.LocalPath));
            File.WriteAllBytes(Path.Combine(_layout.CachePath, "cached.png"), new byte[] { 1, 2, 3 });

            var report = _service.Scan();

            Assert.Equal(1, report.ManagedGamesCount);
            Assert.Equal(2, report.TotalCoversCount);
            Assert.Equal(1, report.ValidCoversCount);
            Assert.True(report.CoverStorageSizeBytes > 0);
            Assert.Equal(3, report.CacheStorageSizeBytes);
            Assert.True(report.HasIssues);
            Assert.Equal(1, report.IssueCount);
        }

        [Fact]
        public void Scan_OnHealthyLibrary_HasNoIssuesEvenWithCacheFiles()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            File.WriteAllBytes(Path.Combine(_layout.CachePath, "cached.png"), new byte[] { 1 });

            var report = _service.Scan();

            Assert.False(report.HasIssues);
            Assert.Equal(0, report.IssueCount);
            Assert.False(report.IsEmpty);
        }

        [Fact]
        public void Scan_NeverDeletesAnything_ByItself()
        {
            var gameId = Guid.NewGuid();
            var orphanDirectory = Path.Combine(_layout.CoversPath, gameId.ToString("N"));
            Directory.CreateDirectory(orphanDirectory);
            var orphanPath = Path.Combine(orphanDirectory, "orphan.png");
            File.WriteAllBytes(orphanPath, new byte[] { 1 });

            _service.Scan();
            _service.Scan();

            Assert.True(File.Exists(orphanPath));
        }
    }
}
