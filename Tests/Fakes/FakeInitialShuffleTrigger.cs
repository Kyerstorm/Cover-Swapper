using System;
using System.Collections.Generic;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>Records every game ID <see cref="CoverImportService"/> reported as having just received a successful import.</summary>
    public class FakeInitialShuffleTrigger : IInitialShuffleTrigger
    {
        public List<Guid> TriggeredGameIds { get; } = new List<Guid>();

        public void TriggerIfNeeded(Guid gameId)
        {
            TriggeredGameIds.Add(gameId);
        }
    }
}
