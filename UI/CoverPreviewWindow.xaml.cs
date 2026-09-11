using System;
using System.Windows;
using System.Windows.Input;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Cover preview: shows one cover at a time from a game's pool, with
    /// Previous/Next navigation and an explicit "Set as Current" action.
    /// Deliberately takes no service dependencies itself - all mutation goes
    /// through the caller-supplied callback captured by
    /// <see cref="CoverPreviewViewModel"/>, so this window never calls back
    /// into any service directly and simply closing it (or navigating
    /// between covers) can never have a side effect on the game or its
    /// covers.
    /// </summary>
    public partial class CoverPreviewWindow : Window
    {
        private readonly CoverPreviewViewModel _viewModel;

        public CoverPreviewWindow(CoverPreviewViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            PreviewKeyDown += CoverPreviewWindow_PreviewKeyDown;
        }

        /// <summary>Left/Right arrow keys move between covers, matching the on-screen Previous/Next buttons, without requiring the mouse.</summary>
        private void CoverPreviewWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                _viewModel.MovePrevious();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                _viewModel.MoveNext();
                e.Handled = true;
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => _viewModel.MovePrevious();

        private void NextButton_Click(object sender, RoutedEventArgs e) => _viewModel.MoveNext();

        private void SetAsCurrentButton_Click(object sender, RoutedEventArgs e) => _viewModel.SetAsCurrent();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
