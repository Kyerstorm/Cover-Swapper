using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Represents one stored cover artwork belonging to a game.
    /// This is a plain domain model; persistence and file handling are
    /// the responsibility of the infrastructure layer.
    /// </summary>
    public class Cover
    {
        /// <summary>Stable identifier for this cover within the plugin's own storage.</summary>
        public Guid CoverId { get; set; }

        /// <summary>The Playnite game ID this cover belongs to.</summary>
        public Guid GameId { get; set; }

        /// <summary>Where this cover originated from.</summary>
        public CoverSource Source { get; set; }

        /// <summary>
        /// Identifier of the cover within its source system (e.g. a SteamGridDB
        /// grid ID). Not used for local files.
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>Path to the cover file within plugin-owned storage.</summary>
        public string LocalPath { get; set; }

        /// <summary>Content hash of the stored file, used to detect duplicates/corruption.</summary>
        public string Hash { get; set; }

        /// <summary>UTC timestamp when this cover was added to the plugin.</summary>
        public DateTime AddedAt { get; set; }

        /// <summary>UTC timestamp when this cover was last applied as the active cover.</summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>Number of times this cover has been applied by the shuffle engine.</summary>
        public int UsageCount { get; set; }

        /// <summary>Whether this cover currently participates in the shuffle pool.</summary>
        public bool IsEnabled { get; set; } = true;
    }
}
