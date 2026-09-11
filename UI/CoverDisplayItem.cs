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

        /// <summary>Whether this is the cover currently applied as the game's Playnite cover.</summary>
        public bool IsCurrent { get; set; }

        /// <summary>Whether this cover's backing file could not be found on disk.</summary>
        public bool IsFileMissing { get; set; }

        /// <summary>1-based position of this cover within the game's pool, for a "Cover #N" label.</summary>
        public int CoverNumber { get; set; }

        /// <summary>Whether "Restore from SteamGridDB" should be offered for this cover.</summary>
        public bool CanRestoreFromSteamGridDb => Source == CoverSource.SteamGridDb;
    }
}
