using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public CoverImportService(ICoverShuffleRepository repository, ICoverStorage storage, ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            var hash = ComputeHash(asset.FilePath);
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
                relativePath = _storage.SaveCoverFile(gameId, coverId, asset.FilePath);
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
            return CoverImportResult.Ok(cover);
        }

        private static bool TryValidateImage(string filePath)
        {
            try
            {
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

        private static string ComputeHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
