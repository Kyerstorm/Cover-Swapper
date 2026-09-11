using System;
using System.Collections.Generic;
using System.Linq;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;

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
        private readonly ICoverStorage _storage;

        public CoverShuffleManager(
            ICoverShuffleRepository repository,
            PlayniteCoverService coverService,
            IPlayniteGameService gameService,
            ICoverStorage storage = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));

            // Optional for backward compatibility with existing test/call
            // sites constructed before Stage 2's "⚠ Missing cover" status
            // existed: without storage, HasMissingCover simply stays false
            // rather than throwing.
            _storage = storage;
        }

        public IReadOnlyList<ManagedGameSummary> GetManagedGames()
        {
            var gameIds = new HashSet<Guid>(_repository.GetGameIdsWithCovers());
            foreach (var configuration in _repository.GetAllGameConfigurations())
            {
                gameIds.Add(configuration.GameId);
            }

            return gameIds
                .Select(gameId =>
                {
                    var covers = _repository.GetCovers(gameId);
                    var state = _repository.GetShuffleState(gameId);

                    // A missing file and an unreadable-but-present file are
                    // distinct "needs attention" conditions (see
                    // ManagedGameSummary.HasCorruptCover), so a cover only
                    // counts toward the corrupt check once it has already
                    // passed the exists check.
                    var hasMissingCover = _storage != null && covers.Any(c => !_storage.CoverFileExists(c.LocalPath));
                    var hasCorruptCover = _storage != null && covers.Any(c =>
                        _storage.CoverFileExists(c.LocalPath) &&
                        !Infrastructure.Storage.CoverImageValidator.IsImageHeaderReadable(_storage.GetAbsolutePath(c.LocalPath)));
                    var hasInvalidCoverReference = state?.CurrentCoverId != null &&
                        covers.All(c => c.CoverId != state.CurrentCoverId.Value);

                    return new ManagedGameSummary
                    {
                        GameId = gameId,
                        GameName = _gameService.GetGameName(gameId) ?? "(game not found in Playnite)",
                        CoverCount = covers.Count,
                        IsEnabled = _coverService.IsEnabled(gameId),
                        HasMissingCover = hasMissingCover,
                        HasCorruptCover = hasCorruptCover,
                        HasInvalidCoverReference = hasInvalidCoverReference,
                        NextShuffleAt = state?.NextShuffleAt,
                        Sources = new HashSet<Domain.CoverSource>(covers.Select(c => c.Source))
                    };
                })
                .OrderBy(g => g.GameName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
