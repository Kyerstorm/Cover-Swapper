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

        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetValue(ref _isEnabled, value, nameof(IsEnabled), nameof(StatusText), nameof(ToggleEnabledButtonText));
        }

        public string StatusText => IsEnabled ? "Enabled" : "Disabled";

        public string ToggleEnabledButtonText => IsEnabled ? "Disable" : "Enable";

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

            Covers.Clear();
            foreach (var cover in _repository.GetCovers(_gameId).OrderBy(c => c.AddedAt))
            {
                Covers.Add(new CoverDisplayItem
                {
                    CoverId = cover.CoverId,
                    AbsoluteImagePath = _storage.GetAbsolutePath(cover.LocalPath),
                    Source = cover.Source,
                    AddedAt = cover.AddedAt,
                    UsageCount = cover.UsageCount,
                    IsCurrent = state?.CurrentCoverId == cover.CoverId
                });
            }

            CoverCountText = $"{Covers.Count} of {CoverLimitPolicy.MaxCoversPerGame} covers";
            IsEnabled = _coverService.IsEnabled(_gameId);
            HasSavedOriginal = _coverService.HasSavedOriginalCover(_gameId);
            CurrentIntervalHours = Math.Round(_coverService.GetEffectiveInterval(_gameId).TotalHours, 1);
            NextShuffleText = ComputeNextShuffleText(state);
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

        public void ShuffleNow()
        {
            var result = _coverService.ShuffleToNextCover(_gameId);
            StatusMessage = result.Success ? "Shuffled to a new cover." : result.Message;
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
    }
}
