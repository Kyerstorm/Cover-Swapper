using System;
using System.Collections.Generic;

namespace PluginCoverShuffle.UI
{
    /// <summary>One selectable row in the Cover Shuffle Manager's game list.</summary>
    public class ManagedGameRow : ObservableObject
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        public int CoverCount { get; set; }

        public string Status => IsEnabled ? "Enabled" : "Disabled";

        private bool _isEnabled;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                SetValue(ref _isEnabled, value);
                OnPropertyChanged(nameof(Status));
            }
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetValue(ref _isSelected, value);
        }
    }
}
