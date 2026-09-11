using System;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Provider-independent cover import pipeline: validates a provider's
    /// <see cref="CoverAsset"/>, stores the image, and registers it as a
    /// <see cref="Cover"/> in the game's shuffle pool. No provider (local
    /// file, SteamGridDB, etc.) should duplicate this logic; they only need
    /// to produce a <see cref="CoverAsset"/> and call <see cref="Import"/>.
    /// </summary>
    public class CoverImportService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly ICoverShuffleLogger _logger;
        private readonly ImageNormalizationService _normalizer;
        private readonly IInitialShuffleTrigger _initialShuffleTrigger;

        public CoverImportService(
            ICoverShuffleRepository repository,
            ICoverStorage storage,
            ICoverShuffleLogger logger,
            ImageNormalizationService normalizer,
            IInitialShuffleTrigger initialShuffleTrigger = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));

            // Optional so every existing test/call site that constructs this
            // service directly (without wiring the full Playnite integration
            // layer) keeps working unchanged; without it, imports simply
            // never auto-apply a first cover.
            _initialShuffleTrigger = initialShuffleTrigger;
        }

        public CoverImportResult Import(Guid gameId, CoverAsset asset)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.FilePath))
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, "No cover file was provided.");
            }

            if (!File.Exists(asset.FilePath))
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, $"File '{asset.FilePath}' could not be found.");
            }

            if (!TryValidateImage(asset.FilePath))
            {
                _logger.Warning($"Rejected invalid cover image '{asset.FilePath}'.");
                return CoverImportResult.Failed(CoverImportStatus.InvalidImage, "The selected file is not a valid image.");
            }

            return WithNormalizedFile(asset.FilePath, workingFilePath =>
            {
                var sizeError = CheckFileSize(workingFilePath);
                if (sizeError != null)
                {
                    return sizeError;
                }

                var hash = CoverHashUtility.ComputeHash(workingFilePath);
                var isDuplicate = _repository.GetCovers(gameId)
                    .Any(c => string.Equals(c.Hash, hash, StringComparison.OrdinalIgnoreCase));
                if (isDuplicate)
                {
                    return CoverImportResult.Failed(CoverImportStatus.DuplicateCover, "This image has already been added to this game's covers.");
                }

                var coverId = Guid.NewGuid();
                string relativePath;
                try
                {
                    relativePath = _storage.SaveCoverFile(gameId, coverId, workingFilePath);
                }
                catch (NotSupportedException ex)
                {
                    return CoverImportResult.Failed(CoverImportStatus.InvalidImage, ex.Message);
                }

                var cover = new Cover
                {
                    CoverId = coverId,
                    GameId = gameId,
                    Source = asset.Source,
                    SourceId = asset.SourceId,
                    LocalPath = relativePath,
                    Hash = hash,
                    AddedAt = DateTime.UtcNow,
                    IsEnabled = true
                };

                try
                {
                    _repository.AddCover(cover);
                }
                catch (CoverLimitExceededException)
                {
                    // The file was already copied into storage; since it never
                    // became a registered cover, removing it is cleanup of our
                    // own stray copy, not deletion of a user's pooled cover.
                    _storage.DeleteCoverFile(relativePath);
                    return CoverImportResult.Failed(
                        CoverImportStatus.CoverLimitExceeded,
                        $"This game already has the maximum of {CoverLimitPolicy.MaxCoversPerGame} covers.");
                }

                _logger.Info($"Imported cover '{coverId}' for game '{gameId}' from {asset.Source}.");

                // Centralized here (rather than in each caller/window) so
                // every import source - local file, SteamGridDB, Playnite
                // Metadata, automatic new-game handling, and any future
                // provider - automatically gets the same "first usable cover
                // becomes the active cover" behaviour with no extra wiring.
                // A no-op for every import after the game's first (see
                // PlayniteCoverService.TryApplyInitialShuffle).
                try
                {
                    _initialShuffleTrigger?.TriggerIfNeeded(gameId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Initial shuffle check failed for game '{gameId}' after importing cover '{coverId}'.");
                }

                return CoverImportResult.Ok(cover);
            });
        }

        /// <summary>
        /// Replaces an existing cover's backing file in place, keeping its
        /// <see cref="Cover.CoverId"/>, <see cref="Cover.AddedAt"/>,
        /// <see cref="Cover.UsageCount"/> and <see cref="Cover.LastUsedAt"/>
        /// history intact. Used to recover a cover whose file was found
        /// missing, rather than re-adding it as a brand new cover.
        /// </summary>
        public CoverImportResult ReplaceFile(Guid gameId, Guid coverId, string newFilePath)
        {
            var existing = _repository.GetCover(gameId, coverId);
            if (existing == null)
            {
                return CoverImportResult.Failed(CoverImportStatus.CoverNotFound, "This cover no longer exists.");
            }

            if (string.IsNullOrWhiteSpace(newFilePath) || !File.Exists(newFilePath))
            {
                return CoverImportResult.Failed(CoverImportStatus.SourceFileMissing, $"File '{newFilePath}' could not be found.");
            }

            if (!TryValidateImage(newFilePath))
            {
                _logger.Warning($"Rejected invalid replacement image '{newFilePath}' for cover '{coverId}'.");
                return CoverImportResult.Failed(CoverImportStatus.InvalidImage, "The selected file is not a valid image.");
            }

            return WithNormalizedFile(newFilePath, workingFilePath =>
            {
                var sizeError = CheckFileSize(workingFilePath);
                if (sizeError != null)
                {
                    return sizeError;
                }

                var hash = CoverHashUtility.ComputeHash(workingFilePath);
                var isDuplicate = _repository.GetCovers(gameId)
                    .Any(c => c.CoverId != coverId && string.Equals(c.Hash, hash, StringComparison.OrdinalIgnoreCase));
                if (isDuplicate)
                {
                    return CoverImportResult.Failed(CoverImportStatus.DuplicateCover, "This image has already been added to this game's covers.");
                }

                string relativePath;
                try
                {
                    // Save the replacement first so a failure here never
                    // destroys the existing cover file. SaveCoverFile
                    // overwrites in place when the extension is unchanged;
                    // the old file is only removed afterward, and only when
                    // it was left behind by an extension change.
                    relativePath = _storage.SaveCoverFile(gameId, coverId, workingFilePath);
                }
                catch (NotSupportedException ex)
                {
                    return CoverImportResult.Failed(CoverImportStatus.InvalidImage, ex.Message);
                }

                if (!string.Equals(relativePath, existing.LocalPath, StringComparison.OrdinalIgnoreCase))
                {
                    _storage.DeleteCoverFile(existing.LocalPath);
                }

                existing.LocalPath = relativePath;
                existing.Hash = hash;
                _repository.UpdateCover(existing);

                _logger.Info($"Replaced the file for cover '{coverId}' (game '{gameId}').");
                return CoverImportResult.Ok(existing);
            });
        }

        private static bool TryValidateImage(string filePath)
        {
            try
            {
                // Read dimensions first without forcing a full pixel-data
                // decode (validateImageData: false), so an image with a
                // small compressed size but an enormous declared resolution
                // is rejected before the expensive full decode below runs.
                using (var probeStream = File.OpenRead(filePath))
                using (var probe = System.Drawing.Image.FromStream(probeStream, useEmbeddedColorManagement: false, validateImageData: false))
                {
                    if (probe.Width > CoverImportPolicy.MaxDecodeDimensionPixels
                        || probe.Height > CoverImportPolicy.MaxDecodeDimensionPixels)
                    {
                        return false;
                    }
                }

                using (var stream = File.OpenRead(filePath))
                using (System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Normalizes <paramref name="sourceFilePath"/>, runs
        /// <paramref name="continueWith"/> against whatever file should
        /// actually be hashed/stored, and always cleans up the temp file
        /// normalization may have produced - the caller's original file is
        /// never touched either way.
        /// </summary>
        private CoverImportResult WithNormalizedFile(string sourceFilePath, Func<string, CoverImportResult> continueWith)
        {
            var normalized = _normalizer.Normalize(sourceFilePath);
            if (!normalized.Success)
            {
                _logger.Warning($"Failed to normalize cover image '{sourceFilePath}': {normalized.ErrorMessage}");
                return CoverImportResult.Failed(CoverImportStatus.InvalidImage, normalized.ErrorMessage);
            }

            try
            {
                return continueWith(normalized.NormalizedFilePath);
            }
            finally
            {
                if (normalized.WasConverted && !string.Equals(normalized.NormalizedFilePath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(normalized.NormalizedFilePath);
                    }
                    catch (IOException)
                    {
                        // Best-effort temp file cleanup; leaving a stray temp
                        // file behind is harmless and shouldn't fail the import.
                    }
                }
            }
        }

        private static CoverImportResult CheckFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > CoverImportPolicy.MaxFileSizeBytes)
            {
                var limitMb = CoverImportPolicy.MaxFileSizeBytes / (1024 * 1024);
                return CoverImportResult.Failed(CoverImportStatus.FileTooLarge, $"This image is larger than the {limitMb} MB limit.");
            }

            return null;
        }
    }
}
