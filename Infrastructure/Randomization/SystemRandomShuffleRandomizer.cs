using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain.Shuffling;

namespace PluginCoverShuffle.Infrastructure.Randomization
{
    /// <summary>
    /// Default <see cref="IShuffleRandomizer"/> for real use: an in-place
    /// Fisher-Yates shuffle backed by a single long-lived <see cref="Random"/>
    /// instance (a fresh instance per call would be seeded from the clock and
    /// risk identical sequences for shuffles happening in quick succession).
    /// </summary>
    public class SystemRandomShuffleRandomizer : IShuffleRandomizer
    {
        private readonly Random _random = new Random();

        public void Shuffle(IList<Guid> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }
    }
}
