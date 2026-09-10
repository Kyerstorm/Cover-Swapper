using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Information captured before Cover Shuffle takes control of a game's
    /// cover, so the original artwork can be restored later. Capturing and
    /// applying this information is the responsibility of the Playnite
    /// integration layer; this type only carries the data to be persisted.
    /// </summary>
    public class OriginalArtworkInfo
    {
        /// <summary>The Playnite game ID this record belongs to.</summary>
        public Guid GameId { get; set; }

        /// <summary>
        /// Reference to the game's original cover artwork as understood by
        /// Playnite (not assumed to be a plain local file path).
        /// </summary>
        public string OriginalCoverReference { get; set; }

        /// <summary>UTC timestamp when the original artwork was captured.</summary>
        public DateTime CapturedAtUtc { get; set; }
    }
}
