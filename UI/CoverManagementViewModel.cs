using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the "Manage Covers" window: the plugin's per-game control
    /// centre. Shows status, scheduling, and the cover pool, and drives
    /// enable/disable, interval, shuffle, and restore actions. Contains no
    /// WPF dependency beyond <see cref="ObservableObject"/>, so it is
    /// testable without instantiating a real window; opening other dialogs
    /// (SteamGridDB search, etc.) for the "Add Cover" buttons is the
    /// window's own responsibility, not this view model's.
    /// </summary>
    public class CoverManagementViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly PlayniteCoverService _coverService;
        private readonly BulkConfigurationService _bulkConfigurationService;

        /// <summary>The Playnite game this panel belongs to.</summary>
        public Guid GameId => _gameId;

        /// <summary>The Playnite game's display name, for the window title/header.</summary>
        public string GameName { get; }

        public ObservableCollection<CoverDisplayItem> Covers { get; } = new ObservableCollection<CoverDisplayItem>();

        private string _coverCountText;

        /// <summary>e.g. "3 of 10 covers" — a quick indicator of how close the game is to the pool limit.</summary>
        public string CoverCountText
        {
            get => _coverCountText;
            set => SetValue(ref _coverCountText, value);
        }

        public bool IsEmpty => Covers.Count == 0;

        private CoverDisplayItem _selectedCover;

        /// <summary>
        /// The cover the user has clicked/focused in the cover grid this
        /// session — distinct from <see cref="CoverDisplayItem.IsCurrent"/>,
        /// which is whatever Playnite is actually showing right now. Drives
        /// the per-selection action bar (Use This Cover / Remove / recovery
        /// actions) so cover cards themselves stay free of nested buttons.
        /// </summary>
        public CoverDisplayItem SelectedCover
        {
            get => _selectedCover;
            set => SetValue(ref _selectedCover, value, nameof(SelectedCover), nameof(HasSelectedCover), nameof(CanApplySelectedCover), nameof(CanRestoreSelectedFromSteamGridDb), nameof(ShowSingleSelectionBar));
        }

        public bool HasSelectedCover => SelectedCover != null;

        /// <summary>Whether "Use This Cover" makes sense for the current selection (not already current, not unusable).</summary>
        public bool CanApplySelectedCover => SelectedCover != null && SelectedCover.CanSetAsCurrent;

        /// <summary>Whether "Restore from SteamGridDB" should be offered for the current selection.</summary>
        public bool CanRestoreSelectedFromSteamGridDb => SelectedCover != null && SelectedCover.IsUnavailable && SelectedCover.CanRestoreFromSteamGridDb;

        private readonly HashSet<Guid> _multiSelectedCoverIds = new HashSet<Guid>();

        /// <summary>
        /// The full multi-selection from the cover grid (Ctrl/Shift-click,
        /// select-all), kept separate from <see cref="SelectedCover"/> (the
        /// grid's single "active" item, used by keyboard Enter/double-click).
        /// Populated by the view's code-behind from
        /// <c>ListBox.SelectedItems</c>, which WPF does not expose as a
        /// bindable property, via <see cref="SetSelection"/>.
        /// </summary>
        public ObservableCollection<CoverDisplayItem> SelectedCovers { get; } = new ObservableCollection<CoverDisplayItem>();

        public int SelectedCoverCount => SelectedCovers.Count;

        public bool HasMultipleSelectedCovers => SelectedCoverCount > 1;

        /// <summary>Whether "Enable" is meaningful for the whole multi-selection (at least one selected cover is currently disabled).</summary>
        public bool CanEnableSelectedCovers => SelectedCovers.Any(c => !c.IsCoverEnabled);

        /// <summary>Whether "Disable" is meaningful for the whole multi-selection (at least one selected cover is currently enabled).</summary>
        public bool CanDisableSelectedCovers => SelectedCovers.Any(c => c.IsCoverEnabled);

        public bool CanRemoveSelectedCovers => SelectedCoverCount > 0;

        /// <summary>Whether the single-cover action bar should show, as opposed to the multi-select bar (mutually exclusive so the two never stack).</summary>
        public bool ShowSingleSelectionBar => HasSelectedCover && !HasMultipleSelectedCovers;

        /// <summary>The cover Playnite is currently showing for this game, if any — for the large "current cover" preview.</summary>
        public CoverDisplayItem CurrentCover { get; private set; }

        public bool HasCurrentCover => CurrentCover != null;

        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetValue(ref _isEnabled, value, nameof(IsEnabled), nameof(StatusText), nameof(ToggleEnabledButtonText));
        }

        public string StatusText => IsEnabled ? "Enabled" : "Disabled";

        public string ToggleEnabledButtonText => IsEnabled ? "Disable" : "Enable";

        private bool _isEnabledOverridden;

        /// <summary>Whether this game has its own enable/disable override, as opposed to following the current global default.</summary>
        public bool IsEnabledOverridden
        {
            get => _isEnabledOverridden;
            private set => SetValue(ref _isEnabledOverridden, value, nameof(IsEnabledOverridden), nameof(EnabledSourceText));
        }

        public string EnabledSourceText => IsEnabledOverridden ? "(custom)" : "(using global default)";

        private bool _hasSavedOriginal;

        public bool HasSavedOriginal
        {
            get => _hasSavedOriginal;
            private set => SetValue(ref _hasSavedOriginal, value);
        }

        private double _currentIntervalHours;

        /// <summary>The interval currently governing this game's scheduled shuffles, in hours (its own override, or the global default).</summary>
        public double CurrentIntervalHours
        {
            get => _currentIntervalHours;
            private set => SetValue(ref _currentIntervalHours, value);
        }

        private bool _isIntervalOverridden;

        /// <summary>Whether this game has its own interval override, as opposed to following the current global default.</summary>
        public bool IsIntervalOverridden
        {
            get => _isIntervalOverridden;
            private set => SetValue(ref _isIntervalOverridden, value, nameof(IsIntervalOverridden), nameof(IntervalSourceText));
        }

        public string IntervalSourceText => IsIntervalOverridden ? "(custom)" : "(inherited from global default)";

        private bool _hasAnyOverride;

        /// <summary>Whether this game has any per-game override at all, i.e. whether "Reset to Global Defaults" has anything to do.</summary>
        public bool HasAnyOverride
        {
            get => _hasAnyOverride;
            private set => SetValue(ref _hasAnyOverride, value);
        }

        private string _nextShuffleText;

        public string NextShuffleText
        {
            get => _nextShuffleText;
            private set => SetValue(ref _nextShuffleText, value);
        }

        private string _statusMessage;

        /// <summary>Transient feedback for the last action (e.g. "Shuffled to a new cover."), shown at the bottom of the panel.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        public CoverManagementViewModel(
            Guid gameId,
            string gameName,
            ICoverShuffleRepository repository,
            ICoverStorage storage,
            PlayniteCoverService coverService,
            BulkConfigurationService bulkConfigurationService)
        {
            _gameId = gameId;
            GameName = string.IsNullOrWhiteSpace(gameName) ? "(game not found in Playnite)" : gameName;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _bulkConfigurationService = bulkConfigurationService ?? throw new ArgumentNullException(nameof(bulkConfigurationService));

            Reload();
        }

        public void Reload()
        {
            var state = _repository.GetShuffleState(_gameId);
            var previouslySelectedCoverId = SelectedCover?.CoverId;
            var previouslyMultiSelectedIds = new HashSet<Guid>(SelectedCovers.Select(c => c.CoverId));

            Covers.Clear();
            var orderedCovers = _repository.GetCovers(_gameId).OrderBy(c => c.AddedAt).ToList();
            for (var i = 0; i < orderedCovers.Count; i++)
            {
                var cover = orderedCovers[i];
                var fileExists = _storage.CoverFileExists(cover.LocalPath);
                Covers.Add(new CoverDisplayItem
                {
                    CoverId = cover.CoverId,
                    AbsoluteImagePath = _storage.GetAbsolutePath(cover.LocalPath),
                    Source = cover.Source,
                    AddedAt = cover.AddedAt,
                    UsageCount = cover.UsageCount,
                    IsCurrent = state?.CurrentCoverId == cover.CoverId,
                    IsFileMissing = !fileExists,
                    IsCorrupt = fileExists && !CoverImageValidator.IsImageHeaderReadable(_storage.GetAbsolutePath(cover.LocalPath)),
                    IsCoverEnabled = cover.IsEnabled,
                    IsFavorite = cover.IsFavorite,
                    CoverNumber = i + 1
                });
            }

            // Cover objects are recreated on every reload, so selection has to be
            // re-matched by id rather than relying on reference equality.
            SelectedCover = previouslySelectedCoverId.HasValue
                ? Covers.FirstOrDefault(c => c.CoverId == previouslySelectedCoverId.Value)
                : null;

            SelectedCovers.Clear();
            foreach (var item in Covers.Where(c => previouslyMultiSelectedIds.Contains(c.CoverId)))
            {
                SelectedCovers.Add(item);
            }

            NotifySelectionChanged();

            CurrentCover = Covers.FirstOrDefault(c => c.IsCurrent);
            OnPropertyChanged(nameof(CurrentCover));
            OnPropertyChanged(nameof(HasCurrentCover));

            CoverCountText = $"{Covers.Count} of {CoverLimitPolicy.MaxCoversPerGame} covers";
            IsEnabled = _coverService.IsEnabled(_gameId);
            HasSavedOriginal = _coverService.HasSavedOriginalCover(_gameId);
            CurrentIntervalHours = Math.Round(_coverService.GetEffectiveInterval(_gameId).TotalHours, 1);
            NextShuffleText = ComputeNextShuffleText(state);

            var overrides = _repository.GetGameConfiguration(_gameId)?.SettingsOverride;
            IsEnabledOverridden = overrides?.Enabled != null;
            IsIntervalOverridden = overrides?.Interval != null;
            HasAnyOverride = overrides != null && (
                overrides.Enabled != null ||
                overrides.Interval != null ||
                overrides.Mode != null ||
                overrides.AvoidConsecutiveDuplicates != null ||
                overrides.ShuffleOnStartup != null ||
                overrides.ShuffleOnGameLaunch != null ||
                overrides.NotificationPreference != null ||
                overrides.NewGameBehavior != null);

            OnPropertyChanged(nameof(IsEmpty));
        }

        private string ComputeNextShuffleText(ShuffleState state)
        {
            if (!IsEnabled)
            {
                return "Not scheduled (disabled)";
            }

            if (state?.NextShuffleAt == null)
            {
                return Covers.Count == 0 ? "Not scheduled (no covers yet)" : "Not shuffled yet";
            }

            return FormatRemaining(state.NextShuffleAt.Value - DateTime.UtcNow);
        }

        /// <summary>Formats a countdown like "12h 43m", "43m", or "Due now" for a non-positive remainder.</summary>
        internal static string FormatRemaining(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                return "Due now";
            }

            var totalMinutes = (int)Math.Ceiling(remaining.TotalMinutes);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0)
            {
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            }

            return $"{minutes}m";
        }

        /// <summary>Enables Cover Shuffle for this game if it is currently disabled, or disables it if enabled.</summary>
        public void ToggleEnabled()
        {
            if (IsEnabled)
            {
                _coverService.DisableCoverShuffle(_gameId);
            }
            else
            {
                _coverService.EnableCoverShuffle(_gameId);
            }

            Reload();
        }

        /// <summary>Applies a new interval override for this game only.</summary>
        public void SetInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
            {
                return;
            }

            _bulkConfigurationService.SetIntervalForAll(new[] { _gameId }, interval);
            Reload();
        }

        /// <summary>Clears every per-game override, so this game follows all current global defaults again.</summary>
        public void ResetToGlobalDefaults()
        {
            _coverService.ResetOverridesToGlobalDefaults(_gameId);
            StatusMessage = "Reset to global defaults.";
            Reload();
        }

        public void ShuffleNow()
        {
            var result = _coverService.ShuffleToNextCover(_gameId);
            StatusMessage = result.Success ? "Shuffled to a new cover." : result.Message;
            Reload();
        }

        /// <summary>Explicitly applies a specific cover, overriding the randomized shuffle cycle.</summary>
        public void ChooseCover(Guid coverId)
        {
            var result = _coverService.ChooseCover(_gameId, coverId);
            StatusMessage = result.Success ? "Cover applied." : result.Message;
            Reload();
        }

        public void RestoreOriginal()
        {
            _coverService.RestoreOriginalCover(_gameId);
            StatusMessage = "Original cover restored.";
            Reload();
        }

        /// <summary>Removes a cover from the shuffle pool. Never deletes the physical file.</summary>
        public void Remove(Guid coverId)
        {
            _repository.RemoveCover(_gameId, coverId);
            Reload();
        }

        /// <summary>
        /// Applies whichever cover is currently selected in the cover grid.
        /// The card grid itself carries no per-card buttons (see
        /// <see cref="SelectedCover"/>), so this is the primary action for a
        /// single click + Enter, or a double click.
        /// </summary>
        public void ChooseSelectedCover()
        {
            if (CanApplySelectedCover)
            {
                ChooseCover(SelectedCover.CoverId);
            }
        }

        /// <summary>Removes whichever cover is currently selected in the cover grid.</summary>
        public void RemoveSelectedCover()
        {
            if (SelectedCover != null)
            {
                Remove(SelectedCover.CoverId);
            }
        }

        /// <summary>
        /// Toggles a cover's shuffle-pool participation without deleting it.
        /// A disabled cover stays stored and can be re-enabled at any time;
        /// see <see cref="Domain.Cover.IsEnabled"/>. Disabling the cover that
        /// happens to be currently displayed is safe: it only removes the
        /// cover from future randomized shuffles (<see cref="PlayniteCoverService.ShuffleToNextCover"/>
        /// already filters by <c>IsEnabled</c>) - it does not touch what
        /// Playnite is currently showing, so the game is never left pointing
        /// at an invalid reference.
        /// </summary>
        public void ToggleCoverEnabled(Guid coverId)
        {
            var cover = _repository.GetCover(_gameId, coverId);
            if (cover == null)
            {
                return;
            }

            cover.IsEnabled = !cover.IsEnabled;
            _repository.UpdateCover(cover);
            StatusMessage = cover.IsEnabled ? "Cover enabled for shuffling." : "Cover disabled; it will stay in the pool but won't be shuffled.";
            Reload();
        }

        /// <summary>Toggles a cover's favourite flag. Purely organizational; never affects shuffle selection.</summary>
        public void ToggleFavorite(Guid coverId)
        {
            var cover = _repository.GetCover(_gameId, coverId);
            if (cover == null)
            {
                return;
            }

            cover.IsFavorite = !cover.IsFavorite;
            _repository.UpdateCover(cover);
            Reload();
        }

        /// <summary>
        /// Replaces the multi-selection tracked for bulk actions. Called by
        /// the view's code-behind from <c>ListBox.SelectionChanged</c>,
        /// since <c>ListBox.SelectedItems</c> is not a bindable property.
        /// </summary>
        public void SetSelection(IEnumerable<CoverDisplayItem> items)
        {
            SelectedCovers.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    SelectedCovers.Add(item);
                }
            }

            NotifySelectionChanged();
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCoverCount));
            OnPropertyChanged(nameof(HasMultipleSelectedCovers));
            OnPropertyChanged(nameof(CanEnableSelectedCovers));
            OnPropertyChanged(nameof(CanDisableSelectedCovers));
            OnPropertyChanged(nameof(CanRemoveSelectedCovers));
            OnPropertyChanged(nameof(ShowSingleSelectionBar));
        }

        /// <summary>Enables every currently multi-selected cover for shuffling, in one batched write.</summary>
        public void EnableSelectedCovers()
        {
            if (SelectedCovers.Count == 0)
            {
                return;
            }

            var ids = SelectedCovers.Select(c => c.CoverId).ToList();
            _repository.ExecuteBatch(() =>
            {
                foreach (var coverId in ids)
                {
                    var cover = _repository.GetCover(_gameId, coverId);
                    if (cover != null && !cover.IsEnabled)
                    {
                        cover.IsEnabled = true;
                        _repository.UpdateCover(cover);
                    }
                }
            });

            StatusMessage = $"Enabled {ids.Count} cover(s) for shuffling.";
            Reload();
        }

        /// <summary>Disables every currently multi-selected cover from shuffling, in one batched write. Never deletes anything.</summary>
        public void DisableSelectedCovers()
        {
            if (SelectedCovers.Count == 0)
            {
                return;
            }

            var ids = SelectedCovers.Select(c => c.CoverId).ToList();
            _repository.ExecuteBatch(() =>
            {
                foreach (var coverId in ids)
                {
                    var cover = _repository.GetCover(_gameId, coverId);
                    if (cover != null && cover.IsEnabled)
                    {
                        cover.IsEnabled = false;
                        _repository.UpdateCover(cover);
                    }
                }
            });

            StatusMessage = $"Disabled {ids.Count} cover(s) from shuffling.";
            Reload();
        }

        /// <summary>
        /// Removes every currently multi-selected cover from the pool, in
        /// one batched write. Only ever touches the plugin's own cover
        /// records - never Playnite's original artwork - and never deletes
        /// the underlying files, matching the single-cover <see cref="Remove"/>.
        /// </summary>
        public void RemoveSelectedCovers()
        {
            if (SelectedCovers.Count == 0)
            {
                return;
            }

            var ids = SelectedCovers.Select(c => c.CoverId).ToList();
            _repository.ExecuteBatch(() =>
            {
                foreach (var coverId in ids)
                {
                    _repository.RemoveCover(_gameId, coverId);
                }
            });

            StatusMessage = $"Removed {ids.Count} cover(s) from the pool.";
            Reload();
        }

    }
}
