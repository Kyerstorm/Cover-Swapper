using System;
using System.Collections.Generic;

namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>The outcome of asking <see cref="IShuffleEngine"/> for the next cover.</summary>
    public class ShuffleAdvanceResult
    {
        public ShuffleAdvanceResult(Guid selectedCoverId, IReadOnlyList<Guid> remainingCycle)
        {
            SelectedCoverId = selectedCoverId;
            RemainingCycle = remainingCycle;
        }

        /// <summary>The cover to apply next.</summary>
        public Guid SelectedCoverId { get; }

        /// <summary>
        /// The covers still queued in the current shuffle cycle after removing
        /// <see cref="SelectedCoverId"/>. Persist this back onto
        /// <see cref="ShuffleState.ShuffleCycle"/> so the cycle survives a restart.
        /// </summary>
        public IReadOnlyList<Guid> RemainingCycle { get; }
    }
}
