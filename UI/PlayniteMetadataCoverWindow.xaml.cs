using System;
using System.Windows;
using System.Windows.Controls;

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
            if (((Button)sender).Tag is SteamGridDbResultItem item)
            {
                await _viewModel.AddAsync(item);
            }
        }
    }
}
