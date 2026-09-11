using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    /// <summary>How the Manager's game list is currently narrowed down. Session-only - not persisted, per Stage 2 scope.</summary>
    public enum ManagedGameFilterMode
    {
        All,
        Enabled,
        Disabled,
        NoCovers,
        NeedsAttention
    }

    /// <summary>How the Manager's game list is currently ordered. Session-only - not persisted, per Stage 2 scope.</summary>
    public enum ManagedGameSortMode
    {
        Name,
        CoverCount,
        EnabledFirst,
        NeedsAttention
    }

    /// <summary>
    /// Backs the Cover Shuffle Manager window: search/filter, bulk
    /// enable/disable/interval, and import/export across every game Cover
    /// Shuffle manages. Contains no WPF dependency beyond
    /// <see cref="ObservableObject"/>, so it is testable without a window.
    /// </summary>
    public class CoverShuffleManagerViewModel : ObservableObject
    {
        private readonly CoverShuffleManager _manager;
        private readonly BulkConfigurationService _bulkConfigurationService;
        private readonly ImportExportService _importExportService;
        private readonly IDialogsFactory _dialogs;

        private List<ManagedGameRow> _allGames = new List<ManagedGameRow>();

        public ObservableCollection<ManagedGameRow> Games { get; } = new ObservableCollection<ManagedGameRow>();

        public bool IsEmpty => _allGames.Count == 0;

        // --- Dashboard header: a simple LINQ rollup over the already-loaded
        // game list, recomputed whenever that list changes. Never a second
        // filesystem/database scan - see CoverShuffleManager.GetManagedGames(),
        // which already gathers all of this in one pass per Reload().
        public int GamesManagedCount => _allGames.Count;

        public int GamesEnabledCount => _allGames.Count(g => g.IsEnabled);

        public int TotalCoversCount => _allGames.Sum(g => g.CoverCount);

        public int GamesNeedingAttentionCount => _allGames.Count(g => g.NeedsAttention);

        public bool HasGamesNeedingAttention => GamesNeedingAttentionCount > 0;

        /// <summary>How many rows are currently checked for a bulk action, independent of which single game is open in the detail pane.</summary>
        public int SelectedGamesCount => _allGames.Count(g => g.IsSelected);

        private ManagedGameRow _selectedGame;

        /// <summary>
        /// The game currently open in the detail pane. Distinct from a
        /// row's <see cref="ManagedGameRow.IsSelected"/> checkbox, which
        /// marks that row for a bulk action and has nothing to do with
        /// which single game's cover pool is on screen.
        /// </summary>
        public ManagedGameRow SelectedGame
        {
            get => _selectedGame;
            set => SetValue(ref _selectedGame, value, nameof(SelectedGame), nameof(HasSelectedGame));
        }

        public bool HasSelectedGame => SelectedGame != null;

        private string _searchText = string.Empty;

        /// <summary>
        /// Pure local, case-insensitive game-name filtering. Must never
        /// trigger a provider (e.g. SteamGridDB) search - this is a filter
        /// over games already known to Cover Shuffle, not an artwork lookup.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetValue(ref _searchText, value ?? string.Empty);
                ApplyFilter();
            }
        }

        private ManagedGameFilterMode _filterMode = ManagedGameFilterMode.All;

        public ManagedGameFilterMode FilterMode
        {
            get => _filterMode;
            set
            {
                SetValue(ref _filterMode, value);
                ApplyFilter();
            }
        }

        private CoverSource? _sourceFilter;

        /// <summary>Restricts the list to games with at least one cover from this source, or null for every source.</summary>
        public CoverSource? SourceFilter
        {
            get => _sourceFilter;
            set
            {
                SetValue(ref _sourceFilter, value);
                ApplyFilter();
            }
        }

        private ManagedGameSortMode _sortMode = ManagedGameSortMode.Name;

        public ManagedGameSortMode SortMode
        {
            get => _sortMode;
            set
            {
                SetValue(ref _sortMode, value);
                ApplyFilter();
            }
        }

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        /// <summary>
        /// "No games match '{search text}'" - distinct from <see cref="IsEmpty"/>
        /// (no games managed at all): this is an empty RESULT of the current
        /// search, over a non-empty underlying list.
        /// </summary>
        public bool ShowNoSearchResults => Games.Count == 0 && _allGames.Count > 0 && !string.IsNullOrWhiteSpace(SearchText);

        /// <summary>
        /// "Everything looks good" - the Needs Attention filter specifically
        /// found nothing, as opposed to there being no games at all or the
        /// search text excluding everything (that case is reported by
        /// <see cref="ShowNoSearchResults"/> instead so the two messages
        /// never both apply at once).
        /// </summary>
        public bool ShowAllHealthy => Games.Count == 0 && _allGames.Count > 0 && string.IsNullOrWhiteSpace(SearchText) &&
            FilterMode == ManagedGameFilterMode.NeedsAttention;

        public CoverShuffleManagerViewModel(
            CoverShuffleManager manager,
            BulkConfigurationService bulkConfigurationService,
            ImportExportService importExportService,
            IDialogsFactory dialogs)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _bulkConfigurationService = bulkConfigurationService ?? throw new ArgumentNullException(nameof(bulkConfigurationService));
            _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

            Reload();
        }

        public void Reload()
        {
            var selectedIds = new HashSet<Guid>(_allGames.Where(g => g.IsSelected).Select(g => g.GameId));
            var previouslyOpenGameId = SelectedGame?.GameId;

            _allGames = _manager.GetManagedGames()
                .Select(summary => new ManagedGameRow
                {
                    GameId = summary.GameId,
                    GameName = summary.GameName,
                    CoverCount = summary.CoverCount,
                    IsEnabled = summary.IsEnabled,
                    HasMissingCover = summary.HasMissingCover,
                    HasCorruptCover = summary.HasCorruptCover,
                    HasInvalidCoverReference = summary.HasInvalidCoverReference,
                    NextShuffleAt = summary.NextShuffleAt,
                    Sources = summary.Sources,
                    IsSelected = selectedIds.Contains(summary.GameId)
                })
                .ToList();

            foreach (var row in _allGames)
            {
                // Bulk-selection checkboxes bind straight to each row's
                // IsSelected (see CoverShuffleGameRowStyle), so this view
                // model only learns about a check/uncheck by listening here
                // - there is no other path back to SelectedGamesCount.
                row.PropertyChanged += Row_PropertyChanged;
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(GamesManagedCount));
            OnPropertyChanged(nameof(GamesEnabledCount));
            OnPropertyChanged(nameof(TotalCoversCount));
            OnPropertyChanged(nameof(GamesNeedingAttentionCount));
            OnPropertyChanged(nameof(HasGamesNeedingAttention));
            OnPropertyChanged(nameof(SelectedGamesCount));
            ApplyFilter();

            // Rows are rebuilt above, so re-match the open detail-pane game by
            // id instead of relying on reference equality surviving reload.
            SelectedGame = previouslyOpenGameId.HasValue
                ? Games.FirstOrDefault(g => g.GameId == previouslyOpenGameId.Value)
                : null;
        }

        /// <summary>
        /// Lightweight refresh of one row's live-changing fields (cover
        /// count, enabled state) after an action in the detail pane, without
        /// rebuilding the whole list/losing the current detail-pane
        /// selection or its transient status message.
        /// </summary>
        public void RefreshGameSummary(Guid gameId)
        {
            var row = _allGames.FirstOrDefault(g => g.GameId == gameId);
            if (row == null)
            {
                return;
            }

            var summary = _manager.GetManagedGames().FirstOrDefault(g => g.GameId == gameId);
            if (summary == null)
            {
                return;
            }

            row.CoverCount = summary.CoverCount;
            row.IsEnabled = summary.IsEnabled;
            row.HasMissingCover = summary.HasMissingCover;
            row.HasCorruptCover = summary.HasCorruptCover;
            row.HasInvalidCoverReference = summary.HasInvalidCoverReference;
            row.NextShuffleAt = summary.NextShuffleAt;
            row.Sources = summary.Sources;

            OnPropertyChanged(nameof(GamesManagedCount));
            OnPropertyChanged(nameof(GamesEnabledCount));
            OnPropertyChanged(nameof(TotalCoversCount));
            OnPropertyChanged(nameof(GamesNeedingAttentionCount));
            OnPropertyChanged(nameof(HasGamesNeedingAttention));

            // Deliberately does NOT call ApplyFilter() here: that clears and
            // rebuilds the visible Games collection, which risks resetting
            // the ListBox's SelectedItem (and, via the TwoWay binding,
            // SelectedGame) even though the underlying row object is
            // unchanged - exactly what this method exists to avoid. A row
            // that newly matches/stops matching the active filter picks that
            // up on the next explicit filter/sort change or full Reload().
        }

        private void Row_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagedGameRow.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedGamesCount));
            }
        }

        private void ApplyFilter()
        {
            Games.Clear();

            IEnumerable<ManagedGameRow> matches = _allGames;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                matches = matches.Where(g => g.GameName != null && g.GameName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            switch (FilterMode)
            {
                case ManagedGameFilterMode.Enabled:
                    matches = matches.Where(g => g.IsEnabled);
                    break;
                case ManagedGameFilterMode.Disabled:
                    matches = matches.Where(g => !g.IsEnabled);
                    break;
                case ManagedGameFilterMode.NoCovers:
                    matches = matches.Where(g => g.CoverCount == 0);
                    break;
                case ManagedGameFilterMode.NeedsAttention:
                    matches = matches.Where(g => g.NeedsAttention);
                    break;
            }

            if (SourceFilter.HasValue)
            {
                matches = matches.Where(g => g.Sources.Contains(SourceFilter.Value));
            }

            matches = ApplySort(matches);

            foreach (var row in matches)
            {
                Games.Add(row);
            }

            OnPropertyChanged(nameof(ShowNoSearchResults));
            OnPropertyChanged(nameof(ShowAllHealthy));
        }

        private IEnumerable<ManagedGameRow> ApplySort(IEnumerable<ManagedGameRow> games)
        {
            switch (SortMode)
            {
                case ManagedGameSortMode.CoverCount:
                    return games.OrderByDescending(g => g.CoverCount).ThenBy(g => g.GameName, StringComparer.OrdinalIgnoreCase);
                case ManagedGameSortMode.EnabledFirst:
                    return games.OrderByDescending(g => g.IsEnabled).ThenBy(g => g.GameName, StringComparer.OrdinalIgnoreCase);
                case ManagedGameSortMode.NeedsAttention:
                    return games.OrderByDescending(g => g.NeedsAttention).ThenBy(g => g.GameName, StringComparer.OrdinalIgnoreCase);
                default:
                    return games.OrderBy(g => g.GameName, StringComparer.OrdinalIgnoreCase);
            }
        }

        private List<Guid> SelectedGameIds() => _allGames.Where(g => g.IsSelected).Select(g => g.GameId).ToList();

        public void EnableSelected()
        {
            var ids = SelectedGameIds();
            if (ids.Count == 0)
            {
                StatusMessage = "Select at least one game first.";
                return;
            }

            _bulkConfigurationService.EnableAll(ids);
            Reload();
            StatusMessage = $"Enabled Cover Shuffle for {ids.Count} game(s).";
        }

        public void DisableSelected()
        {
            var ids = SelectedGameIds();
            if (ids.Count == 0)
            {
                StatusMessage = "Select at least one game first.";
                return;
            }

            _bulkConfigurationService.DisableAll(ids);
            Reload();
            StatusMessage = $"Disabled Cover Shuffle for {ids.Count} game(s).";
        }

        public void SetIntervalForSelected(TimeSpan interval)
        {
            var ids = SelectedGameIds();
            if (ids.Count == 0)
            {
                StatusMessage = "Select at least one game first.";
                return;
            }

            _bulkConfigurationService.SetIntervalForAll(ids, interval);
            StatusMessage = $"Set the shuffle interval for {ids.Count} game(s).";
        }

        /// <summary>
        /// Clears every per-game override for each checked game, so they go
        /// back to following the global defaults for every setting - the
        /// same effect as each game's own "Reset to Global Defaults", just
        /// applied to the whole checked set at once.
        /// </summary>
        public void ResetOverridesForSelected()
        {
            var ids = SelectedGameIds();
            if (ids.Count == 0)
            {
                StatusMessage = "Select at least one game first.";
                return;
            }

            _bulkConfigurationService.ResetOverridesForAll(ids);
            Reload();
            StatusMessage = $"Reset {ids.Count} game(s) to global defaults.";
        }

        public void Export()
        {
            var folder = _dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            var result = _importExportService.Export(folder);
            StatusMessage = result.Message;
        }

        public void Import()
        {
            var folder = _dialogs.SelectFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            var result = _importExportService.Import(folder);
            StatusMessage = result.Message;
            Reload();
        }
    }
}
