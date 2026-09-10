using System;
using System.Windows;

namespace PluginCoverShuffle.UI
{
    public partial class MaintenanceWindow : Window
    {
        private readonly MaintenanceViewModel _viewModel;

        public MaintenanceWindow(MaintenanceViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        private void DeleteOrphanedButton_Click(object sender, RoutedEventArgs e) => _viewModel.DeleteOrphanedFiles();

        private void RemoveInvalidButton_Click(object sender, RoutedEventArgs e) => _viewModel.RemoveInvalidRecords();

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e) => _viewModel.ClearCache();
    }
}
