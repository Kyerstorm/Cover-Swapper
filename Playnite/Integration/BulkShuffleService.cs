using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Shuffles many games in one operation for the Cover Shuffle Manager's
    /// "Shuffle Selected" / "Shuffle All Installed Games" bulk actions.
    /// Applies the same eligibility rules and reuses
    /// <see cref="PlayniteCoverService.ShuffleToNextCover"/> - and therefore
    /// the same <see cref="Domain.Shuffling.IShuffleEngine"/> - for every
    /// game, so there is exactly one randomized-selection implementation in
    /// the whole plugin. A failure on one game never aborts the rest of the
    /// batch, matching <see cref="BulkConfigurationService"/>'s existing
    /// forgiving pattern.
    /// </summary>
    public class BulkShuffleService
    {
        private readonly CoverShuffleManager _manager;
        private readonly PlayniteCoverService _coverService;
        private readonly IPlayniteGameService _gameService;
        private readonly ICoverShuffleLogger _logger;

        public BulkShuffleService(
            CoverShuffleManager manager,
            PlayniteCoverService coverService,
            IPlayniteGameService gameService,
            ICoverShuffleLogger logger)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Shuffles every game Cover Shuffle manages that is currently installed (see <see cref="CoverShuffleManager.GetManagedGameIds"/> for "manages").</summary>
        public BulkShuffleResult ShuffleInstalledGames(IProgress<BulkShuffleProgress> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ShuffleGames(_manager.GetManagedGameIds(), progress, cancellationToken);
        }

        /// <summary>
        /// Shuffles exactly the given games, skipping any that are not
        /// installed, not enabled, or have no usable cover to shuffle from.
        /// Safe to call with an empty or null list.
        /// </summary>
        public BulkShuffleResult ShuffleGames(IEnumerable<Guid> gameIds, IProgress<BulkShuffleProgress> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ids = gameIds?.ToList() ?? new List<Guid>();
            var result = new BulkShuffleResult { Total = ids.Count };

            for (var i = 0; i < ids.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                ProcessGame(ids[i], result);
                progress?.Report(new BulkShuffleProgress { Completed = i + 1, Total = ids.Count });
            }

            return result;
        }

        private void ProcessGame(Guid gameId, BulkShuffleResult result)
        {
            try
            {
                if (!_gameService.IsGameInstalled(gameId))
                {
                    result.SkippedNotInstalled++;
                    return;
                }

                if (!_coverService.IsEnabled(gameId))
                {
                    result.SkippedDisabled++;
                    return;
                }

                var shuffleResult = _coverService.ShuffleToNextCover(gameId);
                if (shuffleResult.Success)
                {
                    result.Shuffled++;
                    return;
                }

                // ShuffleToNextCover only ever fails for one reason: nothing
                // usable to shuffle from (no enabled covers, or every enabled
                // cover's file is missing) - exactly this operation's
                // "no usable covers" eligibility rule, not an unexpected error.
                result.SkippedNoCovers++;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Bulk shuffle failed for game '{gameId}'.");
                result.Failed++;
                result.Failures.Add(new BulkShuffleFailure
                {
                    GameId = gameId,
                    GameName = _gameService.GetGameName(gameId) ?? gameId.ToString(),
                    Message = "An unexpected error occurred. See the Cover Shuffle log for details."
                });
            }
        }
    }
}
