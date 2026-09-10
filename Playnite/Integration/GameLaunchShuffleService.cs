using System;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Reacts to Playnite's game-starting event: shuffles a game's cover
    /// right before it launches, for games with "shuffle on game launch"
    /// in effect. Reuses <see cref="PlayniteCoverService.ShuffleToNextCover"/>
    /// as-is, so a launch shuffle also advances the same scheduled-shuffle
    /// countdown a startup/interval shuffle would — there is only ever one
    /// "next shuffle" clock per game, never a separate one per trigger.
    /// </summary>
    public class GameLaunchShuffleService
    {
        private readonly PlayniteCoverService _coverService;
        private readonly IPlayniteGameService _gameService;
        private readonly ICoverShuffleLogger _logger;
        private readonly CoverShuffleNotificationService _notificationService;

        public GameLaunchShuffleService(
            PlayniteCoverService coverService,
            ICoverShuffleLogger logger,
            IPlayniteGameService gameService = null,
            CoverShuffleNotificationService notificationService = null)
        {
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _gameService = gameService;
            _notificationService = notificationService;
        }

        public void HandleGameStarting(Guid gameId)
        {
            try
            {
                if (!_coverService.IsEnabled(gameId) || !_coverService.GetEffectiveShuffleOnGameLaunch(gameId))
                {
                    return;
                }

                var result = _coverService.ShuffleToNextCover(gameId);
                var gameName = _gameService?.GetGameName(gameId) ?? gameId.ToString();
                var preference = _coverService.GetEffectiveNotificationPreference(gameId);

                if (!result.Success)
                {
                    _logger.Debug($"Launch shuffle skipped for game '{gameId}': {result.Message}");
                    _notificationService?.NotifyShuffleFailed(gameId, gameName, result.Message, preference);
                    return;
                }

                _notificationService?.NotifyShuffled(gameId, gameName, preference);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Handling game-starting shuffle failed for game '{gameId}'.");
            }
        }
    }
}
