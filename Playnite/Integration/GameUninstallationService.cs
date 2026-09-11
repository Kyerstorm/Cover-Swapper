using System;
using System.Linq;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Reacts to Playnite's game-uninstalled event: restores the user's
    /// original Playnite artwork and removes everything Cover Shuffle owns
    /// for that game, so a later reinstall is treated as a brand new game
    /// (see <see cref="GameInstallationService"/>). This only runs from the
    /// genuine Playnite uninstall lifecycle event - never a periodic
    /// "IsInstalled == false" poll, which could delete a user's covers just
    /// because Playnite briefly reported a game as not installed.
    ///
    /// Ordering is safety-critical and intentionally rigid:
    /// restore the original cover and verify it took effect FIRST; only once
    /// that is confirmed does anything Cover Shuffle owns get deleted. If
    /// restoration cannot be verified, nothing is deleted and the game is
    /// left exactly as it was, so a future uninstall event (or manual retry)
    /// can pick up cleanly where this one left off.
    /// </summary>
    public class GameUninstallationService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly IPlayniteGameService _gameService;
        private readonly ICoverShuffleLogger _logger;

        public GameUninstallationService(
            ICoverShuffleRepository repository,
            ICoverStorage storage,
            IPlayniteGameService gameService,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public GameUninstallResult HandleGameUninstalled(Guid gameId, string gameName)
        {
            try
            {
                return CleanUp(gameId, gameName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Uninstall cleanup failed unexpectedly for game '{gameName}' ({gameId}).");
                return GameUninstallResult.UnexpectedFailure(gameId, gameName);
            }
        }

        private GameUninstallResult CleanUp(Guid gameId, string gameName)
        {
            var original = _repository.GetOriginalArtwork(gameId);
            var covers = _repository.GetCovers(gameId);
            var configuration = _repository.GetGameConfiguration(gameId);
            var shuffleState = _repository.GetShuffleState(gameId);

            if (original == null && covers.Count == 0 && configuration == null && shuffleState == null)
            {
                // Cover Shuffle never touched this game (e.g. installed and
                // uninstalled without ever adding a cover) - nothing to
                // restore or delete.
                return GameUninstallResult.ForNothingToDo(gameId, gameName);
            }

            if (!TryRestoreOriginalCover(gameId, gameName, original))
            {
                _logger.Error(
                    $"Uninstall cleanup for game '{gameName}' ({gameId}): could not verify the original cover was restored. " +
                    $"No Cover Shuffle files were deleted; {covers.Count} cover record(s) and all other plugin state remain in place for a future retry.");
                return GameUninstallResult.RestorationFailed(gameId, gameName, covers.Count);
            }

            var filesDeleted = 0;
            var filesFailedToDelete = 0;
            foreach (var cover in covers)
            {
                if (TryDeleteCoverFile(gameId, cover))
                {
                    filesDeleted++;
                }
                else
                {
                    filesFailedToDelete++;
                }
            }

            // Grouped into one batch so a mid-cleanup crash never leaves the
            // database with, say, cover records removed but the shuffle
            // state or original-artwork record still pointing at them.
            _repository.ExecuteBatch(() =>
            {
                foreach (var cover in covers)
                {
                    _repository.RemoveCover(gameId, cover.CoverId);
                }

                _repository.RemoveShuffleState(gameId);
                _repository.ClearOriginalArtwork(gameId);
                _repository.RemoveGameConfiguration(gameId);
            });

            _logger.Info(
                "Uninstall cleanup: " +
                $"Game: {gameName}. Covers: {covers.Count}. Original cover restored: {(original != null ? "yes" : "n/a (none captured)")}. " +
                $"Files deleted: {filesDeleted}{(filesFailedToDelete > 0 ? $" ({filesFailedToDelete} failed)" : string.Empty)}. " +
                $"Records removed: {covers.Count}. Shuffle state removed: yes.");

            return GameUninstallResult.Completed(gameId, gameName, original != null, covers.Count, filesDeleted, filesFailedToDelete);
        }

        /// <summary>
        /// Restores the captured original cover reference (or clears the
        /// cover if the game genuinely had none - <see cref="Domain.OriginalArtworkInfo.OriginalCoverReference"/>
        /// null) and reads it back to confirm Playnite actually applied it,
        /// rather than assuming success just because no exception was thrown.
        /// A game with no captured original at all (Cover Shuffle imported
        /// covers but was never enabled) has nothing to restore, so this
        /// trivially succeeds.
        /// </summary>
        private bool TryRestoreOriginalCover(Guid gameId, string gameName, Domain.OriginalArtworkInfo original)
        {
            if (original == null)
            {
                return true;
            }

            try
            {
                _gameService.SetCoverReference(gameId, original.OriginalCoverReference);
                var appliedReference = _gameService.GetCoverReference(gameId);
                return string.Equals(appliedReference, original.OriginalCoverReference, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Restoring the original cover failed for game '{gameName}' ({gameId}).");
                return false;
            }
        }

        /// <summary>
        /// Deletes one cover's physical file. A file that is already missing
        /// counts as a success (nothing left to clean up); a locked or
        /// otherwise undeletable file is reported as a failure rather than
        /// silently treated as removed - see <see cref="GameUninstallResult.FilesFailedToDelete"/>.
        /// Only ever touches storage the plugin itself owns
        /// (<see cref="ICoverStorage"/> already rejects any path that would
        /// escape plugin storage) - Playnite's own library files are never
        /// reachable from here.
        /// </summary>
        private bool TryDeleteCoverFile(Guid gameId, Domain.Cover cover)
        {
            try
            {
                _storage.DeleteCoverFile(cover.LocalPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, $"Could not delete cover file for cover '{cover.CoverId}' (game '{gameId}') during uninstall cleanup.");
                return false;
            }
        }
    }
}
