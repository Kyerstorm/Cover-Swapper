using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Playnite.SDK;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the Maintenance window: shows what <see cref="MaintenanceService.Scan"/>
    /// found and requires an explicit confirmed click before deleting
    /// anything — never automatic. Contains no WPF dependency beyond
    /// <see cref="ObservableObject"/> and <see cref="IDialogsFactory"/>
    /// (used only for the confirmation prompt), so it is testable without a window.
    /// </summary>
    public class MaintenanceViewModel : ObservableObject
    {
        private readonly MaintenanceService _service;
        private readonly IDialogsFactory _dialogs;
        private MaintenanceReport _report;

        public ObservableCollection<string> OrphanedFiles { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> InvalidRecordDescriptions { get; } = new ObservableCollection<string>();

        public ObservableCollection<string> CacheFiles { get; } = new ObservableCollection<string>();

        public bool IsEmpty => _report != null && _report.IsEmpty;

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        public MaintenanceViewModel(MaintenanceService service, IDialogsFactory dialogs)
        {
            _service = service;
            _dialogs = dialogs;
            Scan();
        }

        public void Scan()
        {
            _report = _service.Scan();

            OrphanedFiles.Clear();
            foreach (var file in _report.OrphanedCoverFiles)
            {
                OrphanedFiles.Add(file);
            }

            InvalidRecordDescriptions.Clear();
            foreach (var cover in _report.InvalidCoverRecords)
            {
                InvalidRecordDescriptions.Add($"Cover {cover.CoverId} (game {cover.GameId}) — file missing");
            }

            CacheFiles.Clear();
            foreach (var file in _report.CacheFiles)
            {
                CacheFiles.Add(file);
            }

            StatusMessage = _report.IsEmpty
                ? "Nothing to clean up."
                : $"{OrphanedFiles.Count} orphaned file(s), {InvalidRecordDescriptions.Count} invalid record(s), {CacheFiles.Count} cache file(s).";

            OnPropertyChanged(nameof(IsEmpty));
        }

        public void DeleteOrphanedFiles()
        {
            if (_report.OrphanedCoverFiles.Count == 0)
            {
                return;
            }

            if (!Confirm($"Permanently delete {_report.OrphanedCoverFiles.Count} orphaned cover file(s) from disk? This cannot be undone."))
            {
                return;
            }

            _service.DeleteOrphanedCoverFiles(_report.OrphanedCoverFiles);
            Scan();
        }

        public void RemoveInvalidRecords()
        {
            if (_report.InvalidCoverRecords.Count == 0)
            {
                return;
            }

            if (!Confirm($"Remove {_report.InvalidCoverRecords.Count} cover record(s) whose file is missing? The (already-missing) files are unaffected."))
            {
                return;
            }

            _service.RemoveInvalidCoverRecords(_report.InvalidCoverRecords);
            Scan();
        }

        public void ClearCache()
        {
            if (_report.CacheFiles.Count == 0)
            {
                return;
            }

            if (!Confirm($"Delete {_report.CacheFiles.Count} cached file(s)? They will be re-downloaded automatically if needed again."))
            {
                return;
            }

            _service.ClearCache();
            Scan();
        }

        private bool Confirm(string message)
        {
            return _dialogs.ShowMessage(message, "Cover Shuffle", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }
    }
}
