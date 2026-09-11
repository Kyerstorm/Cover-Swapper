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
            var flag = IsTruthy(value);
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsTruthy(object value)
        {
            switch (value)
            {
                case bool b:
                    return b;
                case string s:
                    return !string.IsNullOrEmpty(s);
                case int i:
                    return i != 0;
                default:
                    return value != null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
