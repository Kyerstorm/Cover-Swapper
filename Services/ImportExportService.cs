using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Exports Cover Shuffle's configuration and cover images to a portable
    /// folder (CoverShuffle.json plus the referenced image files) and
    /// restores one back in. Intended for moving to another Playnite
    /// installation that shares the same library (so Playnite's game IDs
    /// still match) — e.g. a synced library on a new PC.
    /// </summary>
    public class ImportExportService
    {
        private const string ExportFileName = "CoverShuffle.json";

        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly ICoverShuffleLogger _logger;

        public ImportExportService(ICoverShuffleRepository repository, ICoverStorage storage, ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ImportExportResult Export(string destinationFolder)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                return ImportExportResult.Failed("No destination folder was selected.");
            }

            try
            {
                Directory.CreateDirectory(destinationFolder);

                var exportedCovers = new List<ExportedCover>();
                foreach (var gameId in _repository.GetGameIdsWithCovers())
                {
                    foreach (var cover in _repository.GetCovers(gameId))
                    {
                        var sourcePath = _storage.GetAbsolutePath(cover.LocalPath);
                        if (sourcePath == null || !File.Exists(sourcePath))
                        {
                            _logger.Warning($"Skipping cover '{cover.CoverId}' during export: its file is missing.");
                            continue;
                        }

                        var relativePath = Path.Combine("Covers", gameId.ToString("N"), Path.GetFileName(sourcePath));
                        var destinationPath = Path.Combine(destinationFolder, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(sourcePath, destinationPath, overwrite: true);

                        exportedCovers.Add(new ExportedCover { Cover = cover, RelativeFilePath = relativePath });
                    }
                }

                var export = new CoverShuffleExport
                {
                    ExportedAtUtc = DateTime.UtcNow,
                    GameConfigurations = _repository.GetAllGameConfigurations().ToList(),
                    Covers = exportedCovers
                };

                var json = JsonConvert.SerializeObject(export, Formatting.Indented);
                File.WriteAllText(Path.Combine(destinationFolder, ExportFileName), json);

                _logger.Info($"Exported {export.GameConfigurations.Count} game configuration(s) and {exportedCovers.Count} cover(s) to '{destinationFolder}'.");
                return ImportExportResult.Ok($"Exported {export.GameConfigurations.Count} game configuration(s) and {exportedCovers.Count} cover(s).");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Cover Shuffle export failed.");
                return ImportExportResult.Failed("Export failed. See the Cover Shuffle log for details.");
            }
        }

        public ImportExportResult Import(string sourceFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
            {
                return ImportExportResult.Failed("No source folder was selected.");
            }

            var jsonPath = Path.Combine(sourceFolder, ExportFileName);
            if (!File.Exists(jsonPath))
            {
                return ImportExportResult.Failed($"'{ExportFileName}' was not found in the selected folder.");
            }

            CoverShuffleExport export;
            try
            {
                var json = File.ReadAllText(jsonPath);
                export = JsonConvert.DeserializeObject<CoverShuffleExport>(json);
            }
            catch (JsonException ex)
            {
                _logger.Warning(ex, "Cover Shuffle import file could not be parsed.");
                return ImportExportResult.Failed($"'{ExportFileName}' could not be read; it may be corrupt.");
            }

            if (export == null)
            {
                return ImportExportResult.Failed($"'{ExportFileName}' could not be read; it may be corrupt.");
            }

            try
            {
                var importedConfigurations = 0;
                foreach (var configuration in export.GameConfigurations ?? new List<GameConfiguration>())
                {
                    _repository.SaveGameConfiguration(configuration);
                    importedConfigurations++;
                }

                var importedCovers = 0;
                var skippedCovers = 0;
                foreach (var exportedCover in export.Covers ?? new List<ExportedCover>())
                {
                    if (exportedCover?.Cover == null || string.IsNullOrWhiteSpace(exportedCover.RelativeFilePath))
                    {
                        continue;
                    }

                    // Already present (e.g. re-running the same import) - skip rather than duplicate.
                    if (_repository.GetCover(exportedCover.Cover.GameId, exportedCover.Cover.CoverId) != null)
                    {
                        continue;
                    }

                    var sourcePath = ResolveContainedImportPath(sourceFolder, exportedCover.RelativeFilePath);
                    if (sourcePath == null)
                    {
                        _logger.Warning($"Skipping cover '{exportedCover.Cover.CoverId}' during import: its recorded path is invalid.");
                        skippedCovers++;
                        continue;
                    }

                    if (!File.Exists(sourcePath))
                    {
                        _logger.Warning($"Skipping cover '{exportedCover.Cover.CoverId}' during import: its file is missing from the export.");
                        skippedCovers++;
                        continue;
                    }

                    try
                    {
                        var cover = exportedCover.Cover;
                        cover.LocalPath = _storage.SaveCoverFile(cover.GameId, cover.CoverId, sourcePath);
                        _repository.AddCover(cover);
                        importedCovers++;
                    }
                    catch (CoverLimitExceededException)
                    {
                        _logger.Warning($"Skipping cover '{exportedCover.Cover.CoverId}' during import: game '{exportedCover.Cover.GameId}' is already at the cover limit.");
                        skippedCovers++;
                    }
                    catch (NotSupportedException)
                    {
                        skippedCovers++;
                    }
                }

                var message = $"Imported {importedConfigurations} game configuration(s) and {importedCovers} cover(s).";
                if (skippedCovers > 0)
                {
                    message += $" {skippedCovers} cover(s) were skipped (see the log for details).";
                }

                _logger.Info(message);
                return ImportExportResult.Ok(message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Cover Shuffle import failed.");
                return ImportExportResult.Failed("Import failed partway through. See the Cover Shuffle log for details.");
            }
        }

        /// <summary>
        /// Resolves an imported cover's recorded relative path against
        /// <paramref name="sourceFolder"/>, returning <c>null</c> if the
        /// result would escape that folder (e.g. a tampered "../.."
        /// <c>CoverShuffle.json</c> file) rather than allowing an arbitrary
        /// file elsewhere on disk to be read.
        /// </summary>
        private static string ResolveContainedImportPath(string sourceFolder, string relativeFilePath)
        {
            var rootFull = Path.GetFullPath(sourceFolder);
            var candidateFull = Path.GetFullPath(Path.Combine(sourceFolder, relativeFilePath));

            var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;

            return candidateFull.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
                ? candidateFull
                : null;
        }
    }
}
