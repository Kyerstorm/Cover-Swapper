using System.Collections.Generic;

namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>
    /// Randomization abstraction used by <see cref="IShuffleEngine"/> so the
    /// shuffle logic itself never instantiates <see cref="System.Random"/>
    /// directly and stays deterministic under test.
    /// </summary>
    public interface IShuffleRandomizer
    {
        /// <summary>Randomizes the order of <paramref name="items"/> in place.</summary>
        void Shuffle(IList<System.Guid> items);
    }
}
