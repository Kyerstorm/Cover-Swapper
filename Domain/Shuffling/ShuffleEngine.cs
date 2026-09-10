using System;
using System.Collections.Generic;
using System.Linq;

namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>
    /// Builds randomized shuffle cycles from the enabled cover pool: each
    /// cover is shown once per cycle, a new randomized cycle starts once the
    /// previous one is exhausted, and the first cover of a new cycle is never
    /// the same as the last cover of the previous one (when more than one
    /// cover is available). Covers that were disabled or removed since the
    /// cycle was built are dropped from the queue on the fly; a cover added
    /// mid-cycle naturally joins the next cycle once it is rebuilt.
    /// </summary>
    public class ShuffleEngine : IShuffleEngine
    {
        private readonly IShuffleRandomizer _randomizer;

        public ShuffleEngine(IShuffleRandomizer randomizer)
        {
            _randomizer = randomizer ?? throw new ArgumentNullException(nameof(randomizer));
        }

        public ShuffleAdvanceResult GetNext(IReadOnlyList<Guid> enabledCoverIds, ShuffleState currentState)
        {
            if (enabledCoverIds == null || enabledCoverIds.Count == 0)
            {
                throw new ArgumentException("At least one enabled cover is required to shuffle.", nameof(enabledCoverIds));
            }

            var previousCoverId = currentState?.CurrentCoverId;

            var cycle = (currentState?.ShuffleCycle ?? new List<Guid>())
                .Where(enabledCoverIds.Contains)
                .ToList();

            if (cycle.Count == 0)
            {
                cycle = enabledCoverIds.ToList();
                _randomizer.Shuffle(cycle);

                if (cycle.Count > 1 && previousCoverId.HasValue && cycle[0] == previousCoverId.Value)
                {
                    var swap = cycle[0];
                    cycle[0] = cycle[1];
                    cycle[1] = swap;
                }
            }

            var selected = cycle[0];
            cycle.RemoveAt(0);

            return new ShuffleAdvanceResult(selected, cycle);
        }
    }
}
