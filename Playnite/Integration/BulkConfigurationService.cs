using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Applies enable/disable/interval changes across many games at once for
    /// the Cover Shuffle Manager's bulk actions. A failure on one game is
    /// logged and skipped rather than aborting the rest of the batch.
    /// </summary>
    public class BulkConfigurationService
    {
        private readonly PlayniteCoverService _coverService;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverShuffleLogger _logger;

        public BulkConfigurationService(
            PlayniteCoverService coverService,
            ICoverShuffleRepository repository,
            ICoverShuffleLogger logger)
        {
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void EnableAll(IEnumerable<Guid> gameIds) => ForEachGame(gameIds, _coverService.EnableCoverShuffle);

        public void DisableAll(IEnumerable<Guid> gameIds) => ForEachGame(gameIds, _coverService.DisableCoverShuffle);

        public void SetIntervalForAll(IEnumerable<Guid> gameIds, TimeSpan interval)
        {
            ForEachGame(gameIds, gameId =>
            {
                var configuration = _repository.GetGameConfiguration(gameId) ?? new GameConfiguration { GameId = gameId };
                if (configuration.SettingsOverride == null)
                {
                    configuration.SettingsOverride = new CoverShuffleSettings();
                }

                configuration.SettingsOverride.Interval = interval;
                _repository.SaveGameConfiguration(configuration);
            });
        }

        private void ForEachGame(IEnumerable<Guid> gameIds, Action<Guid> action)
        {
            if (gameIds == null)
            {
                return;
            }

            foreach (var gameId in gameIds)
            {
                try
                {
                    action(gameId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Bulk configuration change failed for game '{gameId}'.");
                }
            }
        }
    }
}
