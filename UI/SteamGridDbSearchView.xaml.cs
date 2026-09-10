using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// The "Add Cover -> SteamGridDB" view. A <see cref="UserControl"/> rather
    /// than a <see cref="Window"/> so it can be hosted inside a window created
    /// by <see cref="IDialogsFactory.CreateWindow"/>, which is what gives the
    /// dialog Playnite's own dark/light theme instead of the plain WPF
    /// default (see <see cref="ShowDialog"/>).
    /// </summary>
    public partial class SteamGridDbSearchView : UserControl
    {
        private readonly SteamGridDbSearchViewModel _viewModel;
        private Window _owningWindow;

        public SteamGridDbSearchView(SteamGridDbSearchViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        /// <summary>
        /// Creates and shows this view in a Playnite-themed modal window. If
        /// the game's name was known, the search field is already populated,
        /// so the initial search runs automatically as soon as the window
        /// opens.
        /// </summary>
        public static void ShowDialog(IDialogsFactory dialogs, SteamGridDbSearchViewModel viewModel)
        {
            if (dialogs == null)
            {
                throw new ArgumentNullException(nameof(dialogs));
            }

            var view = new SteamGridDbSearchView(viewModel);
            var window = dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });

            window.Title = "Add Cover from SteamGridDB";
            window.Content = view;
            window.Height = 640;
            window.Width = 760;
            window.MinHeight = 420;
            window.MinWidth = 560;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            try
            {
                window.Owner = dialogs.GetCurrentAppWindow();
            }
            catch (NotImplementedException)
            {
                // Some IDialogsFactory test doubles don't implement this; the
                // window still works standalone, just without an owner.
            }

            view._owningWindow = window;
            window.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    window.Close();
                }
            };

            window.Loaded += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(viewModel.SearchQuery))
                {
                    await viewModel.SearchAsync();
                }
            };

            window.ShowDialog();
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

        private async void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.AddSelectedAsync();
        }

        private async void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is SteamGridDbResultItem item)
            {
                await _viewModel.AddAsync(item);
            }
        }

        private async void SelectGameMatchButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SelectGameMatchAsync(_viewModel.SelectedGameMatch);
        }

        private async void GameMatchesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ((sender as ListBox)?.SelectedItem is CoverGameMatch match)
            {
                await _viewModel.SelectGameMatchAsync(match);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _owningWindow?.Close();
        }
    }
}
