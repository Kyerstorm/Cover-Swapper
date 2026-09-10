using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // WPF Hyperlinks never open a browser on their own; a bare
            // Process.Start on a URI relies on shell association being
            // registered for http(s), which is true on every supported
            // Windows install this plugin targets.
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
