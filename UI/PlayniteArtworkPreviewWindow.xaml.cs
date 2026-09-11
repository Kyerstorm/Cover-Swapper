using System.Windows;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// A plain, non-modal-state preview of one Playnite metadata artwork
    /// item at a larger size. Deliberately has no view model: unlike
    /// <see cref="CoverPreviewViewModel"/> (which navigates an already-pooled
    /// cover with usage stats and a "Set as Current" action), this only ever
    /// shows a single static image and never mutates anything, so a bindable
    /// model would be pure ceremony.
    /// </summary>
    public partial class PlayniteArtworkPreviewWindow : Window
    {
        public PlayniteArtworkPreviewWindow(string gameName, string artworkDisplayName, string absoluteImagePath)
        {
            InitializeComponent();
            GameNameText.Text = gameName;
            ArtworkNameText.Text = artworkDisplayName;
            PreviewImage.Source = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(absoluteImagePath));
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
