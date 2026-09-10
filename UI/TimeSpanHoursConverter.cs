using System;
using System.Globalization;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>Converts a <see cref="TimeSpan"/> to/from a whole number of hours for display in the settings UI.</summary>
    public class TimeSpanHoursConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan span)
            {
                return Math.Round(span.TotalHours, 1).ToString(CultureInfo.InvariantCulture);
            }

            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) && hours > 0)
            {
                return TimeSpan.FromHours(hours);
            }

            return TimeSpan.FromHours(24);
        }
    }
}
