using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PluginCoverShuffle.UI
{
    public partial class PlayniteMetadataCoverWindow : Window
    {
        private readonly PlayniteMetadataCoverViewModel _viewModel;

        public PlayniteMetadataCoverWindow(PlayniteMetadataCoverViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is PlayniteMetadataArtworkItem item)
            {
                await _viewModel.RetryAsync(item);
            }
        }

        private async void AddAllButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.AddAllAsync();
        }

        /// <summary>Double click previews the artwork at a larger size without adding it - matches the Manager's cover grid ("click = select, double click = preview").</summary>
        private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is PlayniteMetadataArtworkItem item && item.IsAvailable)
            {
                new PlayniteArtworkPreviewWindow(_viewModel.GameName, item.DisplayName, item.Asset.FilePath) { Owner = this }.ShowDialog();
            }
        }

        /// <summary>Enter adds the selected artwork - matches the Manager's "Enter applies the selection" convention.</summary>
        private async void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (sender as ListBox)?.SelectedItem is PlayniteMetadataArtworkItem item)
            {
                await _viewModel.AddAsync(item);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
