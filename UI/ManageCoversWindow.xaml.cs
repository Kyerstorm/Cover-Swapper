using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// The plugin's per-game "Cover Shuffle" control centre: status,
    /// scheduling, the cover pool, and every action available for that one
    /// game. A <see cref="UserControl"/> rather than a <see cref="Window"/>
    /// so it can be hosted inside a window created by
    /// <see cref="IDialogsFactory.CreateWindow"/>, matching Playnite's own
    /// theme instead of the plain WPF default (see <see cref="ShowDialog"/>).
    /// </summary>
    public partial class ManageCoversWindow : UserControl
    {
        private readonly CoverManagementViewModel _viewModel;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverProvider _steamGridDbProvider;
        private readonly ICoverProvider _playniteMetadataProvider;
        private readonly CoverImportService _importService;
        private readonly LocalFileCoverAddService _localFileCoverAddService;
        private readonly ICoverShuffleLogger _logger;
        private IDialogsFactory _dialogs;
        private Window _owningWindow;

        public ManageCoversWindow(
            CoverManagementViewModel viewModel,
            ICoverShuffleRepository repository,
            ICoverProvider steamGridDbProvider,
            ICoverProvider playniteMetadataProvider,
            CoverImportService importService,
            LocalFileCoverAddService localFileCoverAddService,
            ICoverShuffleLogger logger)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _steamGridDbProvider = steamGridDbProvider;
            _playniteMetadataProvider = playniteMetadataProvider;
            _importService = importService;
            _localFileCoverAddService = localFileCoverAddService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            DataContext = _viewModel;

            AddSteamGridDbButton.Visibility = _steamGridDbProvider != null ? Visibility.Visible : Visibility.Collapsed;
            AddPlayniteMetadataButton.Visibility = _playniteMetadataProvider != null ? Visibility.Visible : Visibility.Collapsed;

            RefreshIntervalBox();
        }

        /// <summary>Creates and shows this panel in a Playnite-themed modal window.</summary>
        public static void ShowDialog(
            IDialogsFactory dialogs,
            CoverManagementViewModel viewModel,
            ICoverShuffleRepository repository,
            ICoverProvider steamGridDbProvider,
            ICoverProvider playniteMetadataProvider,
            CoverImportService importService,
            LocalFileCoverAddService localFileCoverAddService,
            ICoverShuffleLogger logger)
        {
            if (dialogs == null)
            {
                throw new ArgumentNullException(nameof(dialogs));
            }

            var view = new ManageCoversWindow(viewModel, repository, steamGridDbProvider, playniteMetadataProvider, importService, localFileCoverAddService, logger);
            view._dialogs = dialogs;

            var window = dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });

            window.Title = $"{viewModel.GameName} - Cover Shuffle";
            window.Content = view;
            window.Height = 680;
            window.Width = 720;
            window.MinHeight = 480;
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

            window.ShowDialog();
        }

        private void RefreshIntervalBox()
        {
            IntervalBox.Text = _viewModel.CurrentIntervalHours.ToString(CultureInfo.InvariantCulture);
        }

        private void ToggleEnabledButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ToggleEnabled();
            RefreshIntervalBox();
        }

        private void ApplyIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(IntervalBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var hours) || hours <= 0)
            {
                _dialogs?.ShowMessage("Enter a valid number of hours greater than zero.");
                return;
            }

            _viewModel.SetInterval(TimeSpan.FromHours(hours));
            RefreshIntervalBox();
        }

        private void ShuffleNowButton_Click(object sender, RoutedEventArgs e) => _viewModel.ShuffleNow();

        private void RestoreOriginalButton_Click(object sender, RoutedEventArgs e) => _viewModel.RestoreOriginal();

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var coverId = (Guid)((Button)sender).Tag;
            _viewModel.Remove(coverId);
        }

        private void AddSteamGridDbButton_Click(object sender, RoutedEventArgs e)
        {
            if (_steamGridDbProvider == null || _importService == null || _dialogs == null)
            {
                return;
            }

            var searchViewModel = new SteamGridDbSearchViewModel(
                _viewModel.GameId, _viewModel.GameName, _steamGridDbProvider, _importService, _repository, _logger);
            SteamGridDbSearchView.ShowDialog(_dialogs, searchViewModel);
            _viewModel.Reload();
        }

        private void AddPlayniteMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playniteMetadataProvider == null || _importService == null)
            {
                return;
            }

            var metadataViewModel = new PlayniteMetadataCoverViewModel(
                _viewModel.GameId, _playniteMetadataProvider, _importService, _repository, _logger);
            new PlayniteMetadataCoverWindow(metadataViewModel) { Owner = _owningWindow }.ShowDialog();
            _viewModel.Reload();
        }

        private async void AddLocalFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_localFileCoverAddService == null || _dialogs == null)
            {
                return;
            }

            var filePath = _dialogs.SelectImagefile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var errorMessage = await System.Threading.Tasks.Task.Run(() => _localFileCoverAddService.AddFromFile(_viewModel.GameId, filePath));
            if (errorMessage != null)
            {
                _dialogs.ShowMessage(errorMessage);
            }

            _viewModel.Reload();
        }
    }
}
