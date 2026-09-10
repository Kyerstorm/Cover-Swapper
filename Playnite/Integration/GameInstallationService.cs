using System;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Reacts to Playnite's game-installed event. Its only job is deciding
    /// whether a game is genuinely new to Cover Shuffle (never configured or
    /// captured before) — a reinstall of an already-known game must not
    /// re-trigger new-game behaviour every time. Actually applying the
    /// configured behaviour is <see cref="NewGameConfigurationService"/>'s job.
    /// </summary>
    public class GameInstallationService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly NewGameConfigurationService _newGameConfigurationService;
        private readonly ICoverShuffleLogger _logger;

        public GameInstallationService(
            ICoverShuffleRepository repository,
            NewGameConfigurationService newGameConfigurationService,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _newGameConfigurationService = newGameConfigurationService ?? throw new ArgumentNullException(nameof(newGameConfigurationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void HandleGameInstalled(Guid gameId, string gameName)
        {
            try
            {
                if (IsAlreadyKnown(gameId))
                {
                    return;
                }

                _newGameConfigurationService.HandleNewGame(gameId, gameName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Handling game-installed event failed for game '{gameId}'.");
            }
        }

        private bool IsAlreadyKnown(Guid gameId)
        {
            return _repository.GetGameConfiguration(gameId) != null || _repository.GetOriginalArtwork(gameId) != null;
        }
    }
}
