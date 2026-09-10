using System;
using System.Globalization;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>Turns the "already added" flag into the Add button's label.</summary>
    public class AlreadyAddedLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool alreadyAdded && alreadyAdded ? "Already Added" : "Add";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
