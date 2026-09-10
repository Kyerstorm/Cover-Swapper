using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PluginCoverShuffle.UI
{
    /// <summary>Binds a radio button's IsChecked to one value of an enum-typed property, via ConverterParameter naming that value.</summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null && parameter != null && value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                return Enum.Parse(targetType, parameter.ToString());
            }

            return Binding.DoNothing;
        }
    }
}
