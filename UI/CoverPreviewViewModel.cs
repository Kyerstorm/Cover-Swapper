using System;
using System.Collections.Generic;
using System.IO;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the cover preview dialog. Purely a display/navigation surface -
    /// moving between covers never mutates anything, and "Set as Current" is
    /// the one explicit, user-initiated exception, which it delegates to the
    /// exact same <see cref="Playnite.Integration.PlayniteCoverService.ChooseCover"/>
    /// path every other "set as current" action already uses (via the
    /// <see cref="_setAsCurrent"/> callback supplied by the caller) rather
    /// than reimplementing it here. Every field shown is data the rest of
    /// the plugin already tracked for this cover; nothing here is invented.
    /// </summary>
    public class CoverPreviewViewModel : ObservableObject
    {
        private readonly IReadOnlyList<CoverDisplayItem> _covers;
        private readonly Action<Guid> _setAsCurrent;
        private int _currentIndex;

        public string GameName { get; }

        /// <summary>Single-cover, read-only compatibility constructor for callers with only one cover and no "set as current" action available.</summary>
        public CoverPreviewViewModel(string gameName, CoverDisplayItem cover)
            : this(gameName, new List<CoverDisplayItem> { cover ?? throw new ArgumentNullException(nameof(cover)) }, 0, null)
        {
        }

        /// <summary>
        /// Full preview experience: navigates within <paramref name="covers"/>
        /// (the game's ordered cover pool) starting at <paramref name="startIndex"/>,
        /// and offers "Set as Current" when <paramref name="setAsCurrent"/> is supplied.
        /// </summary>
        public CoverPreviewViewModel(string gameName, IReadOnlyList<CoverDisplayItem> covers, int startIndex, Action<Guid> setAsCurrent)
        {
            GameName = gameName;
            _covers = covers ?? throw new ArgumentNullException(nameof(covers));
            if (_covers.Count == 0)
            {
                throw new ArgumentException("At least one cover is required.", nameof(covers));
            }

            _setAsCurrent = setAsCurrent;
            _currentIndex = Math.Max(0, Math.Min(startIndex, _covers.Count - 1));
        }

        /// <summary>The cover currently on screen.</summary>
        public CoverDisplayItem Cover => _covers[_currentIndex];

        /// <summary>"Cover N of M" position indicator. Only meaningful (and only shown) when there is more than one cover to navigate.</summary>
        public string PositionText => $"Cover {_currentIndex + 1} of {_covers.Count}";

        /// <summary>Whether the ← Previous / Next → controls should show at all.</summary>
        public bool CanNavigate => _covers.Count > 1;

        public bool CanMovePrevious => _currentIndex > 0;

        public bool CanMoveNext => _currentIndex < _covers.Count - 1;

        public void MovePrevious()
        {
            if (!CanMovePrevious)
            {
                return;
            }

            _currentIndex--;
            RaiseCoverChanged();
        }

        public void MoveNext()
        {
            if (!CanMoveNext)
            {
                return;
            }

            _currentIndex++;
            RaiseCoverChanged();
        }

        /// <summary>Whether "Set as Current" makes sense right now: a callback was supplied, and this specific cover isn't already current/unavailable.</summary>
        public bool CanSetAsCurrent => _setAsCurrent != null && Cover.CanSetAsCurrent;

        private string _statusMessage;

        /// <summary>Lightweight, non-modal feedback for the last preview action (e.g. "Set as current cover.").</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetValue(ref _statusMessage, value);
        }

        /// <summary>
        /// Applies the currently previewed cover through the caller-supplied
        /// callback, then reflects the new current cover locally so the
        /// dialog does not need to close and reopen to show it. Only ever
        /// runs from an explicit click - never automatically on navigation.
        /// </summary>
        public void SetAsCurrent()
        {
            if (!CanSetAsCurrent)
            {
                return;
            }

            var chosenId = Cover.CoverId;
            var chosenInstance = Cover;
            _setAsCurrent(chosenId);

            // The caller's callback (CoverManagementViewModel.ChooseCover)
            // typically reloads the SAME live collection this preview was
            // handed, replacing every CoverDisplayItem with a fresh instance
            // that already carries the correct IsCurrent flags computed from
            // real repository state - re-matching by ID (never by reference,
            // which would no longer match after such a reload) keeps the
            // preview positioned on the same cover either way.
            var matchIndex = -1;
            for (var i = 0; i < _covers.Count; i++)
            {
                if (_covers[i].CoverId == chosenId)
                {
                    matchIndex = i;
                    break;
                }
            }

            if (matchIndex >= 0)
            {
                _currentIndex = matchIndex;
            }

            // Whether the callback actually refreshed the collection is told
            // apart by object identity, not just a matching ID (an ID match
            // happens either way - the cover wasn't removed): if the SAME
            // CoverDisplayItem instance is still there, nothing else has
            // updated its IsCurrent flag, so this is the one place that must.
            var stillSameInstance = matchIndex >= 0 && ReferenceEquals(_covers[matchIndex], chosenInstance);
            if (matchIndex < 0 || stillSameInstance)
            {
                foreach (var cover in _covers)
                {
                    cover.IsCurrent = cover.CoverId == chosenId;
                }
            }

            StatusMessage = "Set as current cover.";
            RaiseCoverChanged();
        }

        public string AbsoluteImagePath => Cover.AbsoluteImagePath;

        public string SourceText => Cover.Source.ToString();

        public string AddedAtText => Cover.AddedAt == default ? "Unknown" : Cover.AddedAt.ToLocalTime().ToString("g");

        public string UsageCountText => $"Used {Cover.UsageCount} time(s)";

        public bool IsCurrent => Cover.IsCurrent;

        public bool IsFavorite => Cover.IsFavorite;

        public bool IsCoverEnabled => Cover.IsCoverEnabled;

        public string HealthStatus => Cover.HealthStatus;

        public bool IsUnavailable => Cover.IsUnavailable;

        public string UnavailableReasonText => Cover.IsFileMissing
            ? "This cover's file could not be found on disk."
            : (Cover.IsCorrupt ? "This cover's file exists but could not be read as a valid image." : null);

        /// <summary>
        /// Pixel dimensions read directly from the file when it can be
        /// opened, or "Unknown" if the file is missing/unreadable - never a
        /// guessed or cached value, since the preview is meant to reflect
        /// exactly what is on disk right now.
        /// </summary>
        public string DimensionsText
        {
            get
            {
                if (Cover.IsUnavailable || string.IsNullOrEmpty(AbsoluteImagePath))
                {
                    return "Unknown";
                }

                try
                {
                    using (var stream = File.OpenRead(AbsoluteImagePath))
                    using (var image = System.Drawing.Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false))
                    {
                        return $"{image.Width} x {image.Height}";
                    }
                }
                catch (Exception)
                {
                    return "Unknown";
                }
            }
        }

        private void RaiseCoverChanged()
        {
            OnPropertyChanged(nameof(Cover));
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(CanMovePrevious));
            OnPropertyChanged(nameof(CanMoveNext));
            OnPropertyChanged(nameof(CanSetAsCurrent));
            OnPropertyChanged(nameof(AbsoluteImagePath));
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(AddedAtText));
            OnPropertyChanged(nameof(UsageCountText));
            OnPropertyChanged(nameof(IsCurrent));
            OnPropertyChanged(nameof(IsFavorite));
            OnPropertyChanged(nameof(IsCoverEnabled));
            OnPropertyChanged(nameof(HealthStatus));
            OnPropertyChanged(nameof(IsUnavailable));
            OnPropertyChanged(nameof(UnavailableReasonText));
            OnPropertyChanged(nameof(DimensionsText));
        }
    }
}
