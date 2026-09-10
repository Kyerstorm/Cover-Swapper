using System.Collections.Generic;

namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>
    /// Decides which cover to show next. Callers are expected to say
    /// "give me the next cover" and persist the returned cycle state; the
    /// engine never touches Playnite or the repository itself.
    /// </summary>
    public interface IShuffleEngine
    {
        /// <summary>
        /// Returns the next cover to apply, consuming one entry from the
        /// current shuffle cycle (building and randomizing a new cycle first
        /// if the previous one is exhausted or stale).
        /// </summary>
        /// <param name="enabledCoverIds">Covers currently eligible to be shown, in any order.</param>
        /// <param name="currentState">
        /// The game's persisted shuffle state, or null if none exists yet.
        /// </param>
        ShuffleAdvanceResult GetNext(IReadOnlyList<System.Guid> enabledCoverIds, ShuffleState currentState);
    }
}
