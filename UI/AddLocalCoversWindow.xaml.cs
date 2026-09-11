using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// The "Add Local Covers" dialog: drag &amp; drop or browse for one or more
    /// local image files, preview per-file status, and import the valid
    /// ones. A <see cref="UserControl"/> rather than a <see cref="Window"/>
    /// so it can be hosted inside a window created by
    /// <see cref="IDialogsFactory.CreateWindow"/>, matching Playnite's own
    /// theme (see <see cref="ShowDialog"/>). All decision logic (validity,
    /// duplicates, capacity) lives in <see cref="AddLocalCoversViewModel"/>;
    /// this code-behind only translates WPF events into calls on it.
    /// </summary>
    public partial class AddLocalCoversWindow : UserControl
    {
        private readonly AddLocalCoversViewModel _viewModel;
        private IDialogsFactory _dialogs;
        private Window _owningWindow;

        public AddLocalCoversWindow(AddLocalCoversViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        /// <summary>Creates and shows this dialog in a Playnite-themed modal window.</summary>
        public static void ShowDialog(IDialogsFactory dialogs, AddLocalCoversViewModel viewModel, Window owner)
        {
            if (dialogs == null)
            {
                throw new ArgumentNullException(nameof(dialogs));
            }

            var view = new AddLocalCoversWindow(viewModel);
            view._dialogs = dialogs;

            var window = dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });

            window.Title = "Add Local Covers";
            window.Content = view;
            window.Height = 560;
            window.Width = 680;
            window.MinHeight = 420;
            window.MinWidth = 560;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (owner != null)
            {
                window.Owner = owner;
            }
            else
            {
                try
                {
                    window.Owner = dialogs.GetCurrentAppWindow();
                }
                catch (NotImplementedException)
                {
                    // Some IDialogsFactory test doubles don't implement this; the
                    // window still works standalone, just without an owner.
                }
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

        private void BrowseFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_dialogs == null)
            {
                return;
            }

            var extensionPatterns = string.Join(";", CoverImportPolicy.AllowedExtensions.Select(ext => "*" + ext));
            var filter = $"Image files|{extensionPatterns}";
            var files = _dialogs.SelectFiles(filter);
            if (files != null && files.Count > 0)
            {
                _viewModel.AddCandidateFiles(files);
            }
        }

        private void RootControl_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                _viewModel.IsDragActive = true;
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void RootControl_DragLeave(object sender, DragEventArgs e)
        {
            _viewModel.IsDragActive = false;
        }

        private void RootControl_Drop(object sender, DragEventArgs e)
        {
            _viewModel.IsDragActive = false;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
            _viewModel.AddCandidateFiles(paths.Where(File.Exists));
        }

        private async void ImportSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ImportSelectedAsync();
        }

        private void RemoveCandidateButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).Tag is LocalFileImportCandidate candidate)
            {
                _viewModel.RemoveCandidate(candidate);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _owningWindow?.Close();
        }
    }
}
