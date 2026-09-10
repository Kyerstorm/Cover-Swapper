using System;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>One row of the Cover Shuffle Manager's game list.</summary>
    public class ManagedGameSummary
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        public int CoverCount { get; set; }

        public bool IsEnabled { get; set; }
    }
}
