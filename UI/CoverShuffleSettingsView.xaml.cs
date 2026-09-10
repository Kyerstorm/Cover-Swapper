using System.Windows;
using System.Windows.Controls;
using PluginCoverShuffle.Settings;

namespace PluginCoverShuffle.UI
{
    public partial class CoverShuffleSettingsView : UserControl
    {
        public CoverShuffleSettingsView()
        {
            InitializeComponent();
        }

        private async void ValidateApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CoverShufflePluginSettingsViewModel viewModel)
            {
                await viewModel.ValidateApiKeyAsync();
            }
        }
    }
}
