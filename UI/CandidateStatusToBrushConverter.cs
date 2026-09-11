using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PluginCoverShuffle.UI
{
    /// <summary>Colors a <see cref="LocalFileCandidateStatus"/> badge: green for ready/imported, amber for a resize/convert notice, red for anything blocking import.</summary>
    public class CandidateStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush GoodBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush WarnBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
        private static readonly SolidColorBrush BadBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is LocalFileCandidateStatus status))
            {
                return Brushes.Gray;
            }

            switch (status)
            {
                case LocalFileCandidateStatus.Valid:
                case LocalFileCandidateStatus.Imported:
                    return GoodBrush;
                case LocalFileCandidateStatus.Pending:
                    return Brushes.Gray;
                default:
                    return status == LocalFileCandidateStatus.WillExceedLimit ? WarnBrush : BadBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
