using System;
using System.Windows;

namespace PluginCoverShuffle.UI
{
    public partial class BulkShuffleProgressWindow : Window
    {
        private readonly BulkShuffleProgressViewModel _viewModel;

        public BulkShuffleProgressWindow(BulkShuffleProgressViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            Title = _viewModel.Title;

            // Closing the window (e.g. the title bar's X) while a shuffle is
            // still running behaves the same as pressing Cancel, rather than
            // silently abandoning the operation.
            Closing += (s, e) => _viewModel.Cancel();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.RunAsync();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => _viewModel.Cancel();

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e) => _viewModel.ShowDetails = !_viewModel.ShowDetails;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
