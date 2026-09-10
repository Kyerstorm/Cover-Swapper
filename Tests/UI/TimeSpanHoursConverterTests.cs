using System;
using System.Globalization;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class TimeSpanHoursConverterTests
    {
        private readonly TimeSpanHoursConverter _converter = new TimeSpanHoursConverter();

        [Fact]
        public void Convert_TimeSpan_ProducesHoursString()
        {
            var result = _converter.Convert(TimeSpan.FromHours(24), typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal("24", result);
        }

        [Fact]
        public void ConvertBack_ValidHours_ProducesTimeSpan()
        {
            var result = _converter.ConvertBack("12", typeof(TimeSpan), null, CultureInfo.InvariantCulture);

            Assert.Equal(TimeSpan.FromHours(12), result);
        }

        [Fact]
        public void ConvertBack_InvalidInput_FallsBackToDefault()
        {
            var result = _converter.ConvertBack("not-a-number", typeof(TimeSpan), null, CultureInfo.InvariantCulture);

            Assert.Equal(TimeSpan.FromHours(24), result);
        }

        [Fact]
        public void ConvertBack_ZeroOrNegative_FallsBackToDefault()
        {
            var result = _converter.ConvertBack("0", typeof(TimeSpan), null, CultureInfo.InvariantCulture);

            Assert.Equal(TimeSpan.FromHours(24), result);
        }
    }
}
