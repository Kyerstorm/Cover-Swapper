using System;
using System.Collections.Generic;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Domain.Shuffling
{
    public class ShuffleEngineTests
    {
        [Fact]
        public void GetNext_WithNoEnabledCovers_Throws()
        {
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());

            Assert.Throws<ArgumentException>(() => engine.GetNext(new List<Guid>(), null));
        }

        [Fact]
        public void GetNext_WithNoPriorState_BuildsAndConsumesFromANewCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());

            var result = engine.GetNext(new List<Guid> { a, b }, null);

            Assert.Equal(a, result.SelectedCoverId);
            Assert.Equal(new[] { b }, result.RemainingCycle);
        }

        [Fact]
        public void GetNext_ConsumesEachCoverExactlyOncePerCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var c = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());

            var seen = new List<Guid>();
            ShuffleState state = null;
            for (var i = 0; i < 3; i++)
            {
                var result = engine.GetNext(new List<Guid> { a, b, c }, state);
                seen.Add(result.SelectedCoverId);
                state = new ShuffleState { CurrentCoverId = result.SelectedCoverId, ShuffleCycle = result.RemainingCycle.ToList() };
            }

            Assert.Equal(new[] { a, b, c }, seen);
        }

        [Fact]
        public void GetNext_WhenCycleExhausted_StartsANewRandomizedCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());
            var state = new ShuffleState { CurrentCoverId = b, ShuffleCycle = new List<Guid>() };

            var result = engine.GetNext(new List<Guid> { a, b }, state);

            Assert.Equal(a, result.SelectedCoverId);
        }

        [Fact]
        public void GetNext_AvoidsImmediateRepetitionAcrossCycleBoundary()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());
            var state = new ShuffleState { CurrentCoverId = a, ShuffleCycle = new List<Guid>() };

            // With only two covers and an identity randomizer, the freshly
            // built cycle is [a, b] — its first entry equals the previous
            // pick (a), so the engine must swap to avoid repeating it.
            var result = engine.GetNext(new List<Guid> { a, b }, state);

            Assert.Equal(b, result.SelectedCoverId);
        }

        [Fact]
        public void GetNext_DropsCoversNoLongerEnabled_FromAQueuedCycle()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var removed = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());
            // 'removed' was queued before being disabled/deleted.
            var state = new ShuffleState { CurrentCoverId = null, ShuffleCycle = new List<Guid> { removed, a, b } };

            var result = engine.GetNext(new List<Guid> { a, b }, state);

            Assert.Equal(a, result.SelectedCoverId);
            Assert.Equal(new[] { b }, result.RemainingCycle);
        }

        [Fact]
        public void GetNext_WithASingleEnabledCover_KeepsSelectingIt()
        {
            var only = Guid.NewGuid();
            var engine = new ShuffleEngine(new FakeShuffleRandomizer());
            var state = new ShuffleState { CurrentCoverId = only, ShuffleCycle = new List<Guid>() };

            var result = engine.GetNext(new List<Guid> { only }, state);

            Assert.Equal(only, result.SelectedCoverId);
            Assert.Empty(result.RemainingCycle);
        }
    }
}
