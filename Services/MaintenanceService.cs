using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Finds and, only when explicitly asked, cleans up orphaned cover
    /// files, cover records whose file is missing, and cached provider
    /// images. <see cref="Scan"/> never deletes anything by itself — every
    /// deletion is a separate method the caller must invoke after the user
    /// has seen the findings and confirmed.
    /// </summary>
    public class MaintenanceService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverStorageLayout _layout;
        private readonly ICoverShuffleLogger _logger;

        public MaintenanceService(
            ICoverShuffleRepository repository,
            ICoverStorage storage,
            CoverStorageLayout layout,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MaintenanceReport Scan()
        {
            var report = new MaintenanceReport();
            var knownRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var gameIds = _repository.GetGameIdsWithCovers();
            report.ManagedGamesCount = gameIds.Count;

            foreach (var gameId in gameIds)
            {
                foreach (var cover in _repository.GetCovers(gameId))
                {
                    report.TotalCoversCount++;
                    knownRelativePaths.Add(NormalizeRelativePath(cover.LocalPath));
                    if (!_storage.CoverFileExists(cover.LocalPath))
                    {
                        report.InvalidCoverRecords.Add(cover);
                    }
                    else
                    {
                        report.CoverStorageSizeBytes += SafeFileLength(_storage.GetAbsolutePath(cover.LocalPath));
                    }
                }
            }

            foreach (var filePath in SafeEnumerateFiles(_layout.CoversPath))
            {
                var relative = NormalizeRelativePath(GetRelativeToRoot(filePath));
                if (!knownRelativePaths.Contains(relative))
                {
                    report.OrphanedCoverFiles.Add(filePath);
                    report.CoverStorageSizeBytes += SafeFileLength(filePath);
                }
            }

            foreach (var filePath in SafeEnumerateFiles(_layout.CachePath))
            {
                report.CacheFiles.Add(filePath);
                report.CacheStorageSizeBytes += SafeFileLength(filePath);
            }

            return report;
        }

        /// <summary>
        /// A file can disappear or become briefly inaccessible between being
        /// listed and having its size read (another process, a race with the
        /// user's own cleanup). Scan results are diagnostic, so a size lookup
        /// failure degrades to "0 bytes for this file" instead of failing the
        /// whole scan.
        /// </summary>
        private long SafeFileLength(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                return 0;
            }

            try
            {
                return new FileInfo(absolutePath).Length;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _logger.Warning(ex, $"Could not read the size of '{absolutePath}' during a maintenance scan.");
                return 0;
            }
        }

        /// <summary>
        /// Directory.GetFiles can throw for reasons outside the user's
        /// control (permissions, a file briefly locked by another process,
        /// the folder having been removed externally). A scan is diagnostic,
        /// not critical, so a listing failure degrades to "found nothing
        /// here" instead of failing the whole scan.
        /// </summary>
        private IEnumerable<string> SafeEnumerateFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            try
            {
                return Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _logger.Warning(ex, $"Could not list files under '{directory}' during a maintenance scan.");
                return Enumerable.Empty<string>();
            }
        }

        /// <summary>Permanently deletes the given files. Caller must have already confirmed with the user.</summary>
        public void DeleteOrphanedCoverFiles(IEnumerable<string> absoluteFilePaths)
        {
            foreach (var filePath in absoluteFilePaths ?? Enumerable.Empty<string>())
            {
                TryDelete(filePath);
            }
        }

        /// <summary>Removes cover records whose file is missing. Caller must have already confirmed with the user.</summary>
        public void RemoveInvalidCoverRecords(IEnumerable<Cover> covers)
        {
            foreach (var cover in covers ?? Enumerable.Empty<Cover>())
            {
                try
                {
                    _repository.RemoveCover(cover.GameId, cover.CoverId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to remove invalid cover record '{cover.CoverId}'.");
                }
            }
        }

        /// <summary>Deletes every cached provider image. Always safe to regenerate; caller must still have confirmed with the user.</summary>
        public void ClearCache()
        {
            if (!Directory.Exists(_layout.CachePath))
            {
                return;
            }

            DeleteCacheFiles(Directory.GetFiles(_layout.CachePath, "*", SearchOption.AllDirectories));
        }

        /// <summary>Deletes the given cached files. Always safe to regenerate; caller must still have confirmed with the user.</summary>
        public void DeleteCacheFiles(IEnumerable<string> absoluteFilePaths)
        {
            foreach (var filePath in absoluteFilePaths ?? Enumerable.Empty<string>())
            {
                TryDelete(filePath);
            }
        }

        private void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to delete '{filePath}' during maintenance cleanup.");
            }
        }

        private string GetRelativeToRoot(string absolutePath)
        {
            return absolutePath.Length > _layout.RootPath.Length
                ? absolutePath.Substring(_layout.RootPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : absolutePath;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return (relativePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
