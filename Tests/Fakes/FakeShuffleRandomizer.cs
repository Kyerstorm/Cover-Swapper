using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain.Shuffling;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>
    /// Deterministic <see cref="IShuffleRandomizer"/> for tests. Defaults to
    /// leaving order untouched; pass a custom <paramref name="behavior"/> to
    /// exercise a specific cycle ordering.
    /// </summary>
    public class FakeShuffleRandomizer : IShuffleRandomizer
    {
        private readonly Action<IList<Guid>> _behavior;

        public FakeShuffleRandomizer(Action<IList<Guid>> behavior = null)
        {
            _behavior = behavior;
        }

        public void Shuffle(IList<Guid> items)
        {
            _behavior?.Invoke(items);
        }
    }
}
