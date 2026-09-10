using System;
using System.Windows;
using System.Windows.Controls;

namespace PluginCoverShuffle.UI
{
    public partial class ManageCoversWindow : Window
    {
        private readonly CoverManagementViewModel _viewModel;

        public ManageCoversWindow(CoverManagementViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var coverId = (Guid)((Button)sender).Tag;
            _viewModel.Remove(coverId);
        }
    }
}
