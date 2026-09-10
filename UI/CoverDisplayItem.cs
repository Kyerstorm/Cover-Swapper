using System;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.UI
{
    /// <summary>Read-only projection of a <see cref="Cover"/> for display in the management window.</summary>
    public class CoverDisplayItem
    {
        public Guid CoverId { get; set; }

        public string AbsoluteImagePath { get; set; }

        public CoverSource Source { get; set; }

        public DateTime AddedAt { get; set; }

        public int UsageCount { get; set; }
    }
}
