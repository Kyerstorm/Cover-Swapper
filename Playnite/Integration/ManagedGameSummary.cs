using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>One row of the Cover Shuffle Manager's game list.</summary>
    public class ManagedGameSummary
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        public int CoverCount { get; set; }

        public bool IsEnabled { get; set; }

        /// <summary>Whether at least one of this game's covers has a missing backing file, computed from real storage/repository state (never a heuristic).</summary>
        public bool HasMissingCover { get; set; }

        /// <summary>
        /// Whether at least one of this game's covers exists on disk but
        /// fails the same cheap header-decode probe used by the per-game
        /// Manage Covers window, computed from real file state (never a
        /// heuristic). Distinct from <see cref="HasMissingCover"/>.
        /// </summary>
        public bool HasCorruptCover { get; set; }

        /// <summary>
        /// Whether the persisted shuffle state's current cover ID no longer
        /// corresponds to any cover actually stored for this game (e.g. the
        /// cover was removed outside the normal "Remove" flow, or data
        /// became stale). Real, repository-backed state - never a guess.
        /// </summary>
        public bool HasInvalidCoverReference { get; set; }

        /// <summary>
        /// UTC timestamp of when this game's next scheduled shuffle is due,
        /// straight from the persisted <see cref="Domain.ShuffleState"/> -
        /// null when no shuffle has ever run or none is scheduled. Never
        /// invented or estimated.
        /// </summary>
        public DateTime? NextShuffleAt { get; set; }

        /// <summary>The distinct set of sources (SteamGridDB/Local/Playnite metadata) this game's covers came from, for the Manager's provider filter.</summary>
        public HashSet<CoverSource> Sources { get; set; } = new HashSet<CoverSource>();
    }
}
