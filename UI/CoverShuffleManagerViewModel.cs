using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Playnite.SDK;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
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

        private string _searchText = string.Empty;

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetValue(ref _searchText, value ?? string.Empty);
                ApplyFilter();
            }
        }

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

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

            _allGames = _manager.GetManagedGames()
                .Select(summary => new ManagedGameRow
                {
                    GameId = summary.GameId,
                    GameName = summary.GameName,
                    CoverCount = summary.CoverCount,
                    IsEnabled = summary.IsEnabled,
                    IsSelected = selectedIds.Contains(summary.GameId)
                })
                .ToList();

            OnPropertyChanged(nameof(IsEmpty));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Games.Clear();
            var matches = string.IsNullOrWhiteSpace(SearchText)
                ? _allGames
                : _allGames.Where(g => g.GameName != null && g.GameName.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var row in matches)
            {
                Games.Add(row);
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
