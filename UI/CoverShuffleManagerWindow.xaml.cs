using System;
using System.Globalization;
using System.Windows;
using Playnite.SDK;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    public partial class CoverShuffleManagerWindow : Window
    {
        private readonly CoverShuffleManagerViewModel _viewModel;
        private readonly MaintenanceService _maintenanceService;
        private readonly IDialogsFactory _dialogs;

        public CoverShuffleManagerWindow(
            CoverShuffleManagerViewModel viewModel,
            MaintenanceService maintenanceService,
            IDialogsFactory dialogs)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            DataContext = _viewModel;
        }

        private void EnableButton_Click(object sender, RoutedEventArgs e) => _viewModel.EnableSelected();

        private void DisableButton_Click(object sender, RoutedEventArgs e) => _viewModel.DisableSelected();

        private void SetIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(IntervalBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours) || hours <= 0)
            {
                _dialogs.ShowMessage("Enter a valid number of hours greater than zero.");
                return;
            }

            _viewModel.SetIntervalForSelected(TimeSpan.FromHours(hours));
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e) => _viewModel.Export();

        private void ImportButton_Click(object sender, RoutedEventArgs e) => _viewModel.Import();

        private void MaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            var maintenanceViewModel = new MaintenanceViewModel(_maintenanceService, _dialogs);
            new MaintenanceWindow(maintenanceViewModel) { Owner = this }.ShowDialog();
            _viewModel.Reload();
        }
    }
}
