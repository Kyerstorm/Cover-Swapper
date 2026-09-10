using System;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
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
        private readonly IShuffleEngine _shuffleEngine;

        public PlayniteCoverService(
            ICoverShuffleRepository repository,
            IPlayniteGameService gameService,
            ICoverStorage storage,
            Func<CoverShuffleSettings> globalSettingsProvider,
            ICoverShuffleLogger logger,
            IShuffleEngine shuffleEngine)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _globalSettingsProvider = globalSettingsProvider ?? throw new ArgumentNullException(nameof(globalSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _shuffleEngine = shuffleEngine ?? throw new ArgumentNullException(nameof(shuffleEngine));
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
        /// Advances the game's cover to the next cover selected by the
        /// <see cref="IShuffleEngine"/>'s randomized cycle, persisting the
        /// remaining cycle so it survives a restart.
        /// </summary>
        public ShuffleResult ShuffleToNextCover(Guid gameId)
        {
            var covers = _repository.GetCovers(gameId).Where(c => c.IsEnabled).ToList();
            if (covers.Count == 0)
            {
                return ShuffleResult.Failed("This game has no covers configured yet. Use \"Add Cover\" to add one.");
            }

            // Applying a cover overwrites Game.CoverImage, so the true
            // original must be captured first even if Enable was never used.
            CaptureOriginalCoverIfMissing(gameId);

            var coverIds = covers.Select(c => c.CoverId).ToList();
            var state = _repository.GetShuffleState(gameId);
            var cycleState = state;

            // A cover's file can vanish outside the plugin (manual deletion,
            // a sync tool, antivirus quarantine). Applying a reference to a
            // missing file would leave Playnite showing a broken image, so
            // skip such covers rather than apply them; each skip still
            // consumes its slot from the cycle so it isn't retried forever.
            for (var attempt = 0; attempt < coverIds.Count; attempt++)
            {
                var advance = _shuffleEngine.GetNext(coverIds, cycleState);
                var selected = covers.First(c => c.CoverId == advance.SelectedCoverId);

                if (!_storage.CoverFileExists(selected.LocalPath))
                {
                    _logger.Warning($"Cover '{selected.CoverId}' for game '{gameId}' is missing its file; skipping it. Run Maintenance to clean up invalid cover records.");
                    cycleState = new ShuffleState
                    {
                        GameId = gameId,
                        CurrentCoverId = state?.CurrentCoverId,
                        LastShuffleAt = state?.LastShuffleAt,
                        NextShuffleAt = state?.NextShuffleAt,
                        ShuffleCycle = advance.RemainingCycle.ToList()
                    };
                    continue;
                }

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
                    NextShuffleAt = now.Add(GetEffectiveInterval(gameId)),
                    ShuffleCycle = advance.RemainingCycle.ToList()
                });

                _logger.Info($"Shuffled to cover '{selected.CoverId}' for game '{gameId}'.");
                return ShuffleResult.Ok();
            }

            return ShuffleResult.Failed(
                "None of this game's covers could be found on disk. Open \"Manage Covers\" or run Maintenance to clean up missing covers.");
        }

        /// <summary>The interval currently governing this game's scheduled shuffles: its own override, or the global default.</summary>
        public TimeSpan GetEffectiveInterval(Guid gameId) => ResolveEffectiveSettings(gameId).Interval;

        /// <summary>How much shuffle activity for this game should be surfaced as a Playnite notification: its own override, or the global default.</summary>
        public NotificationPreference GetEffectiveNotificationPreference(Guid gameId) => ResolveEffectiveSettings(gameId).NotificationPreference;

        /// <summary>Whether this game should shuffle its cover each time it launches: its own override, or the global default.</summary>
        public bool GetEffectiveShuffleOnGameLaunch(Guid gameId) => ResolveEffectiveSettings(gameId).ShuffleOnGameLaunch;

        /// <summary>
        /// A per-game override, when saved, is a complete settings snapshot
        /// (not a sparse patch), so it is used as-is in full; otherwise the
        /// current global default applies.
        /// </summary>
        private CoverShuffleSettings ResolveEffectiveSettings(Guid gameId)
        {
            return _repository.GetGameConfiguration(gameId)?.SettingsOverride
                ?? _globalSettingsProvider()
                ?? new CoverShuffleSettings();
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

        /// <summary>Sets a per-game interval override, independent of enable state.</summary>
        public void SetIntervalOverride(Guid gameId, TimeSpan interval)
        {
            var configuration = GetOrCreateConfigurationWithOverride(gameId);
            configuration.SettingsOverride.Interval = interval;
            _repository.SaveGameConfiguration(configuration);
        }

        private void SetEnabled(Guid gameId, bool enabled)
        {
            var configuration = GetOrCreateConfigurationWithOverride(gameId);
            configuration.SettingsOverride.Enabled = enabled;
            _repository.SaveGameConfiguration(configuration);
        }

        /// <summary>
        /// A game's first per-game override must start as a full snapshot of
        /// the current global settings, not a bare <see cref="CoverShuffleSettings"/>
        /// with type defaults — otherwise changing one setting for a game
        /// (e.g. enabling it, or giving it its own interval) would silently
        /// reset every other setting for that game away from whatever the
        /// user has configured globally.
        /// </summary>
        private GameConfiguration GetOrCreateConfigurationWithOverride(Guid gameId)
        {
            var configuration = _repository.GetGameConfiguration(gameId) ?? new GameConfiguration { GameId = gameId };
            if (configuration.SettingsOverride == null)
            {
                configuration.SettingsOverride = CloneGlobalSettings();
            }

            return configuration;
        }

        private CoverShuffleSettings CloneGlobalSettings()
        {
            var global = _globalSettingsProvider() ?? new CoverShuffleSettings();
            return new CoverShuffleSettings
            {
                Enabled = global.Enabled,
                Interval = global.Interval,
                Mode = global.Mode,
                AvoidConsecutiveDuplicates = global.AvoidConsecutiveDuplicates,
                ShuffleOnStartup = global.ShuffleOnStartup,
                ShuffleOnGameLaunch = global.ShuffleOnGameLaunch,
                NotificationPreference = global.NotificationPreference,
                SteamGridDbApiKey = global.SteamGridDbApiKey,
                NewGameBehavior = global.NewGameBehavior
            };
        }
    }
}
