using System;
using System.Globalization;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>Renders a cover's favourite state as a filled or outline star glyph.</summary>
    public class FavoriteGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isFavorite && isFavorite ? "★" : "☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
