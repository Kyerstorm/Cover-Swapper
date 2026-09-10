using System;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// The core "is a shuffle due, and if so apply it" check for one game.
    /// Reusable from any trigger (startup today; a future in-session timer
    /// or game-launch hook could call the same method).
    /// </summary>
    public class ScheduledShuffleService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly PlayniteCoverService _coverService;
        private readonly ICoverShuffleLogger _logger;

        public ScheduledShuffleService(
            ICoverShuffleRepository repository,
            PlayniteCoverService coverService,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Applies exactly one shuffle if the game is enabled and due, and
        /// always leaves the schedule advanced for next time as part of that
        /// single shuffle. Never performs more than one shuffle per call,
        /// regardless of how long Playnite was closed.
        /// </summary>
        public void ShuffleIfDue(Guid gameId)
        {
            if (!_coverService.IsEnabled(gameId))
            {
                return;
            }

            var state = _repository.GetShuffleState(gameId);
            var isDue = state?.NextShuffleAt == null || state.NextShuffleAt.Value <= DateTime.UtcNow;
            if (!isDue)
            {
                return;
            }

            var result = _coverService.ShuffleToNextCover(gameId);
            if (!result.Success)
            {
                _logger.Debug($"Scheduled shuffle skipped for game '{gameId}': {result.Message}");
            }
        }
    }
}
