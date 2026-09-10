using System;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Coordinates taking control of and restoring a game's cover artwork.
    /// This is the only place that decides when Cover Shuffle is allowed to
    /// overwrite a game's cover and when the original must be restored.
    /// </summary>
    public class PlayniteCoverService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly IPlayniteGameService _gameService;
        private readonly ICoverStorage _storage;
        private readonly Func<CoverShuffleSettings> _globalSettingsProvider;
        private readonly ICoverShuffleLogger _logger;

        public PlayniteCoverService(
            ICoverShuffleRepository repository,
            IPlayniteGameService gameService,
            ICoverStorage storage,
            Func<CoverShuffleSettings> globalSettingsProvider,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _globalSettingsProvider = globalSettingsProvider ?? throw new ArgumentNullException(nameof(globalSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Whether Cover Shuffle is currently enabled for this specific game.</summary>
        public bool IsEnabled(Guid gameId)
        {
            return _repository.GetGameConfiguration(gameId)?.SettingsOverride?.Enabled ?? false;
        }

        /// <summary>Whether the original cover has been captured and can be restored.</summary>
        public bool HasSavedOriginalCover(Guid gameId)
        {
            return _repository.GetOriginalArtwork(gameId) != null;
        }

        /// <summary>
        /// Takes control of the game's cover: captures the current cover as
        /// the restorable original (only the first time, so a later shuffled
        /// cover is never mistaken for the original) and marks the game as
        /// enabled.
        /// </summary>
        public void EnableCoverShuffle(Guid gameId)
        {
            CaptureOriginalCoverIfMissing(gameId);
            SetEnabled(gameId, true);
            _logger.Info($"Cover Shuffle enabled for game '{gameId}'.");
        }

        /// <summary>
        /// Stops future shuffling for this game. Does not change the
        /// currently displayed cover; use <see cref="RestoreOriginalCover"/>
        /// to put the original artwork back.
        /// </summary>
        public void DisableCoverShuffle(Guid gameId)
        {
            SetEnabled(gameId, false);
            _logger.Info($"Cover Shuffle disabled for game '{gameId}'.");
        }

        /// <summary>
        /// Restores the game's originally captured cover, if one was saved.
        /// Safe to call even if no original was ever captured.
        /// </summary>
        public void RestoreOriginalCover(Guid gameId)
        {
            var original = _repository.GetOriginalArtwork(gameId);
            if (original == null)
            {
                _logger.Warning($"No original cover is saved for game '{gameId}'; nothing to restore.");
                return;
            }

            _gameService.SetCoverReference(gameId, original.OriginalCoverReference);
            _logger.Info($"Restored original cover for game '{gameId}'.");
        }

        /// <summary>
        /// Advances the game's cover to the next enabled cover in the pool
        /// (by add order), wrapping around. This is a minimal, deterministic
        /// "next cover" step for the local-file provider phase; it is not
        /// the randomized-cycle shuffle engine described for a later phase,
        /// which will replace this once it exists.
        /// </summary>
        public ShuffleResult ShuffleToNextCover(Guid gameId)
        {
            var covers = _repository.GetCovers(gameId).Where(c => c.IsEnabled).OrderBy(c => c.AddedAt).ToList();
            if (covers.Count == 0)
            {
                return ShuffleResult.Failed("This game has no covers configured yet. Use \"Add Cover\" to add one.");
            }

            // Applying a cover overwrites Game.CoverImage, so the true
            // original must be captured first even if Enable was never used.
            CaptureOriginalCoverIfMissing(gameId);

            var state = _repository.GetShuffleState(gameId);
            var currentIndex = state?.CurrentCoverId != null
                ? covers.FindIndex(c => c.CoverId == state.CurrentCoverId.Value)
                : -1;
            var nextIndex = (currentIndex + 1) % covers.Count;
            var selected = covers[nextIndex];

            _gameService.SetCoverReference(gameId, _storage.GetAbsolutePath(selected.LocalPath));

            selected.LastUsedAt = DateTime.UtcNow;
            selected.UsageCount += 1;
            _repository.UpdateCover(selected);

            var now = DateTime.UtcNow;
            _repository.SaveShuffleState(new ShuffleState
            {
                GameId = gameId,
                CurrentCoverId = selected.CoverId,
                LastShuffleAt = now,
                NextShuffleAt = now.Add(ResolveInterval(gameId))
            });

            _logger.Info($"Shuffled to cover '{selected.CoverId}' for game '{gameId}'.");
            return ShuffleResult.Ok();
        }

        /// <summary>
        /// A per-game override, when saved, is a complete settings snapshot
        /// (not a sparse patch), so its Interval is used as-is; otherwise the
        /// current global default applies.
        /// </summary>
        private TimeSpan ResolveInterval(Guid gameId)
        {
            var overrideSettings = _repository.GetGameConfiguration(gameId)?.SettingsOverride;
            if (overrideSettings != null)
            {
                return overrideSettings.Interval;
            }

            return _globalSettingsProvider()?.Interval ?? TimeSpan.FromHours(24);
        }

        private void CaptureOriginalCoverIfMissing(Guid gameId)
        {
            if (_repository.GetOriginalArtwork(gameId) != null)
            {
                return;
            }

            var currentCover = _gameService.GetCoverReference(gameId);
            _repository.SaveOriginalArtwork(new OriginalArtworkInfo
            {
                GameId = gameId,
                OriginalCoverReference = currentCover,
                CapturedAtUtc = DateTime.UtcNow
            });
            _logger.Debug($"Captured original cover for game '{gameId}'.");
        }

        private void SetEnabled(Guid gameId, bool enabled)
        {
            var configuration = _repository.GetGameConfiguration(gameId) ?? new GameConfiguration { GameId = gameId };
            if (configuration.SettingsOverride == null)
            {
                configuration.SettingsOverride = new CoverShuffleSettings();
            }

            configuration.SettingsOverride.Enabled = enabled;
            _repository.SaveGameConfiguration(configuration);
        }
    }
}
