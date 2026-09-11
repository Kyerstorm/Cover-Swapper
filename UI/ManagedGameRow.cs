using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.UI
{
    /// <summary>One selectable row in the Cover Shuffle Manager's game list.</summary>
    public class ManagedGameRow : ObservableObject
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        private int _coverCount;

        public int CoverCount
        {
            get => _coverCount;
            set
            {
                SetValue(ref _coverCount, value);
                OnPropertyChanged(nameof(NeedsAttention));
                OnPropertyChanged(nameof(AttentionReasonText));
            }
        }

        public string Status => IsEnabled ? "Enabled" : "Disabled";

        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                SetValue(ref _isEnabled, value);
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(NeedsAttention));
                OnPropertyChanged(nameof(NextShuffleText));
            }
        }

        private bool _hasMissingCover;

        /// <summary>Whether at least one of this game's covers has a missing backing file. Real, repository/storage-backed data - never a guess.</summary>
        public bool HasMissingCover
        {
            get => _hasMissingCover;
            set
            {
                SetValue(ref _hasMissingCover, value);
                OnPropertyChanged(nameof(NeedsAttention));
                OnPropertyChanged(nameof(AttentionReasonText));
            }
        }

        private bool _hasCorruptCover;

        /// <summary>Whether at least one of this game's covers exists but fails the header-decode probe. Real, storage-backed data - never a guess.</summary>
        public bool HasCorruptCover
        {
            get => _hasCorruptCover;
            set
            {
                SetValue(ref _hasCorruptCover, value);
                OnPropertyChanged(nameof(NeedsAttention));
                OnPropertyChanged(nameof(AttentionReasonText));
            }
        }

        private bool _hasInvalidCoverReference;

        /// <summary>Whether the persisted "current cover" reference no longer matches any stored cover for this game. Real, repository-backed data - never a guess.</summary>
        public bool HasInvalidCoverReference
        {
            get => _hasInvalidCoverReference;
            set
            {
                SetValue(ref _hasInvalidCoverReference, value);
                OnPropertyChanged(nameof(NeedsAttention));
                OnPropertyChanged(nameof(AttentionReasonText));
            }
        }

        private DateTime? _nextShuffleAt;

        /// <summary>UTC timestamp of this game's next scheduled shuffle, straight from persisted state - null when unknown/not scheduled.</summary>
        public DateTime? NextShuffleAt
        {
            get => _nextShuffleAt;
            set
            {
                SetValue(ref _nextShuffleAt, value);
                OnPropertyChanged(nameof(NextShuffleText));
            }
        }

        /// <summary>
        /// Compact "Next: 1h 24m" hint for the row subtitle, reusing the
        /// exact same countdown formatting as the per-game detail pane
        /// (<see cref="CoverManagementViewModel.FormatRemaining"/>) so the
        /// two screens never disagree on wording. Omitted entirely (rather
        /// than invented) when the game is disabled or nothing is scheduled
        /// yet - never claim a next-shuffle time that isn't actually
        /// persisted.
        /// </summary>
        public string NextShuffleText => IsEnabled && NextShuffleAt.HasValue
            ? "Next: " + CoverManagementViewModel.FormatRemaining(NextShuffleAt.Value - DateTime.UtcNow)
            : null;

        /// <summary>The distinct cover sources for this game, for the Manager's provider filter.</summary>
        public HashSet<CoverSource> Sources { get; set; } = new HashSet<CoverSource>();

        /// <summary>
        /// Whether this game shows up under the "Needs Attention"
        /// filter/sort. Only real, actionable conditions count: a missing
        /// cover file, a cover that exists but cannot be decoded, a stale/
        /// invalid current-cover reference, or the game having zero covers
        /// at all. Deliberately excludes valid configurations that merely
        /// look sparse - fewer than the 10-cover limit, exactly one cover,
        /// or shuffle simply being disabled are NOT problems.
        /// </summary>
        public bool NeedsAttention => HasMissingCover || HasCorruptCover || HasInvalidCoverReference || CoverCount == 0;

        /// <summary>
        /// Short explanation of why this row is flagged, for the row's
        /// warning line and the Needs Attention filter. Priority order
        /// (most actionable first) mirrors <see cref="CoverDisplayItem.HealthStatus"/>.
        /// Null when the game does not need attention.
        /// </summary>
        public string AttentionReasonText
        {
            get
            {
                if (HasMissingCover)
                {
                    return "Missing cover file";
                }

                if (HasCorruptCover)
                {
                    return "Corrupt cover file";
                }

                if (HasInvalidCoverReference)
                {
                    return "Invalid cover reference";
                }

                if (CoverCount == 0)
                {
                    return "No covers";
                }

                return null;
            }
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetValue(ref _isSelected, value);
        }
    }
}
