using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>True/non-empty shows the element; false/empty collapses it. Pass ConverterParameter="Invert" to flip that.</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
