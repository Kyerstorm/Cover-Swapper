using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PluginCoverShuffle.UI
{
    public partial class SteamGridDbSearchWindow : Window
    {
        private readonly SteamGridDbSearchViewModel _viewModel;

        public SteamGridDbSearchWindow(SteamGridDbSearchViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SearchAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await _viewModel.SearchAsync();
            }
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
