using System;
using System.Collections.Generic;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Persisted per-game scheduling and cycle state for the shuffle engine.
    /// This is derived and stored independently of any UI state.
    /// </summary>
    public class ShuffleState
    {
        /// <summary>The Playnite game ID this state belongs to.</summary>
        public Guid GameId { get; set; }

        /// <summary>The cover currently applied as the game's Playnite cover.</summary>
        public Guid? CurrentCoverId { get; set; }

        /// <summary>UTC timestamp of the last time a shuffle was applied.</summary>
        public DateTime? LastShuffleAt { get; set; }

        /// <summary>UTC timestamp of when the next shuffle is due.</summary>
        public DateTime? NextShuffleAt { get; set; }

        /// <summary>
        /// Remaining cover IDs in the current randomized shuffle cycle, in the
        /// order they will be consumed. Emptied and rebuilt once fully consumed.
        /// </summary>
        public List<Guid> ShuffleCycle { get; set; } = new List<Guid>();
    }
}
