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

            foreach (var gameId in _repository.GetGameIdsWithCovers())
            {
                foreach (var cover in _repository.GetCovers(gameId))
                {
                    knownRelativePaths.Add(NormalizeRelativePath(cover.LocalPath));
                    if (!_storage.CoverFileExists(cover.LocalPath))
                    {
                        report.InvalidCoverRecords.Add(cover);
                    }
                }
            }

            foreach (var filePath in SafeEnumerateFiles(_layout.CoversPath))
            {
                var relative = NormalizeRelativePath(GetRelativeToRoot(filePath));
                if (!knownRelativePaths.Contains(relative))
                {
                    report.OrphanedCoverFiles.Add(filePath);
                }
            }

            report.CacheFiles.AddRange(SafeEnumerateFiles(_layout.CachePath));

            return report;
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

            foreach (var filePath in Directory.GetFiles(_layout.CachePath, "*", SearchOption.AllDirectories))
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
