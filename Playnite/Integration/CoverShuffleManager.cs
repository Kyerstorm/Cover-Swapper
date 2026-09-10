using System;
using System.Collections.Generic;
using System.Linq;
using PluginCoverShuffle.Infrastructure.Persistence;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Lists every game Cover Shuffle currently manages — anything with a
    /// saved <see cref="Domain.GameConfiguration"/> or at least one stored
    /// cover, since a game can accumulate covers without ever having been
    /// explicitly enabled. Backs the Cover Shuffle Manager window.
    /// </summary>
    public class CoverShuffleManager
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly PlayniteCoverService _coverService;
        private readonly IPlayniteGameService _gameService;

        public CoverShuffleManager(
            ICoverShuffleRepository repository,
            PlayniteCoverService coverService,
            IPlayniteGameService gameService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
        }

        public IReadOnlyList<ManagedGameSummary> GetManagedGames()
        {
            var gameIds = new HashSet<Guid>(_repository.GetGameIdsWithCovers());
            foreach (var configuration in _repository.GetAllGameConfigurations())
            {
                gameIds.Add(configuration.GameId);
            }

            return gameIds
                .Select(gameId => new ManagedGameSummary
                {
                    GameId = gameId,
                    GameName = _gameService.GetGameName(gameId) ?? "(game not found in Playnite)",
                    CoverCount = _repository.GetCovers(gameId).Count,
                    IsEnabled = _coverService.IsEnabled(gameId)
                })
                .OrderBy(g => g.GameName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
