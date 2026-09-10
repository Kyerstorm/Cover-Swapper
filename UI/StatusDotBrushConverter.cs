using System;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>Colors the enabled/disabled status dot: green when Cover Shuffle is enabled, the theme's muted text color otherwise.</summary>
    public class StatusDotBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush EnabledBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isEnabled && isEnabled ? (Brush)EnabledBrush : Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
