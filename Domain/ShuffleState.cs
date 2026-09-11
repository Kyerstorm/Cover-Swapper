using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain.Shuffling;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Persisted per-game scheduling and cycle state for the shuffle engine.
    /// This is derived and stored independently of any UI state.
    /// </summary>
    public class ShuffleState
    {
        public ShuffleState()
        {
        }

        /// <summary>Creates an independent copy of <paramref name="source"/>.</summary>
        public ShuffleState(ShuffleState source)
        {
            GameId = source.GameId;
            CurrentCoverId = source.CurrentCoverId;
            LastShuffleAt = source.LastShuffleAt;
            NextShuffleAt = source.NextShuffleAt;
            ShuffleCycle = new List<Guid>(source.ShuffleCycle);
            LastShuffleTrigger = source.LastShuffleTrigger;
        }

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

        /// <summary>
        /// Whether <see cref="CurrentCoverId"/> was selected by the randomized
        /// engine or explicitly chosen by the user. Missing values from older
        /// persisted data default to <see cref="ShuffleTrigger.Random"/>, which
        /// matches the actual historical behaviour before this field existed.
        /// </summary>
        public ShuffleTrigger LastShuffleTrigger { get; set; } = ShuffleTrigger.Random;
    }
}
