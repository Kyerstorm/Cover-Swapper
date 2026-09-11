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

        /// <summary>
        /// Whether the file exists but could not be decoded as a valid image
        /// (e.g. truncated download, corrupted disk sector). Distinct from
        /// <see cref="IsFileMissing"/>: both are surfaced as
        /// "cover unavailable" states, but corruption needs a different
        /// diagnostic message since the file is present. Only checked for
        /// files that exist, using a cheap header-only probe (no full pixel
        /// decode) so this stays safe to compute on every reload for the
        /// small (max 10) per-game cover pool.
        /// </summary>
        public bool IsCorrupt { get; set; }

        /// <summary>Whether this cover currently participates in the randomized shuffle pool (<see cref="Cover.IsEnabled"/>).</summary>
        public bool IsCoverEnabled { get; set; }

        /// <summary>
        /// Whether the user has marked this cover as a favourite. Purely an
        /// organizational/UI concept - see <see cref="Cover.IsFavorite"/>.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>1-based position of this cover within the game's pool, for a "Cover #N" label.</summary>
        public int CoverNumber { get; set; }

        /// <summary>Whether "Restore from SteamGridDB" should be offered for this cover.</summary>
        public bool CanRestoreFromSteamGridDb => Source == CoverSource.SteamGridDb;

        /// <summary>Whether the file is missing or unreadable, i.e. this cover cannot currently be applied.</summary>
        public bool IsUnavailable => IsFileMissing || IsCorrupt;

        /// <summary>Short label for the missing/corrupt overlay shown on the card.</summary>
        public string UnavailableReasonText => IsFileMissing ? "Missing" : (IsCorrupt ? "Unavailable" : null);

        /// <summary>Whether "Set as Current"/"Use This Cover" makes sense for this specific card (not already current, not unusable).</summary>
        public bool CanSetAsCurrent => !IsCurrent && !IsUnavailable;

        /// <summary>"● Active" / "○ Disabled" label for the per-cover shuffle-participation state.</summary>
        public string CoverEnabledStatusText => IsCoverEnabled ? "● Active" : "○ Disabled";

        /// <summary>
        /// Single-value rollup used for compact status display and for the
        /// "Needs Attention" family of filters/sorts. Computed purely from
        /// data already loaded for the card - no extra scan. Priority order
        /// (most actionable first): a cover that cannot be applied at all
        /// outranks one that merely isn't shuffle-eligible, which outranks
        /// purely descriptive state (current/favourite).
        /// </summary>
        public string HealthStatus
        {
            get
            {
                if (IsFileMissing)
                {
                    return "Missing file";
                }

                if (IsCorrupt)
                {
                    return "Cover unavailable";
                }

                if (!IsCoverEnabled)
                {
                    return "Disabled";
                }

                if (IsCurrent)
                {
                    return "Current";
                }

                if (IsFavorite)
                {
                    return "Favourite";
                }

                return "Healthy";
            }
        }

        /// <summary>Whether this card's health rollup indicates it needs the user's attention.</summary>
        public bool NeedsAttention => IsFileMissing || IsCorrupt;

        /// <summary>Screen-reader text for this card, since the image itself carries no alt text.</summary>
        public string AccessibleDescription
        {
            get
            {
                var description = $"Cover {CoverNumber}, {Source}";
                if (IsCurrent)
                {
                    description += ", currently applied";
                }

                if (IsFavorite)
                {
                    description += ", favourite";
                }

                if (!IsCoverEnabled)
                {
                    description += ", disabled";
                }

                if (IsFileMissing)
                {
                    description += ", file missing";
                }
                else if (IsCorrupt)
                {
                    description += ", cover unavailable";
                }

                return description;
            }
        }
    }
}
