using System;
using PluginCoverShuffle.Domain;
using Xunit;

namespace PluginCoverShuffle.Tests.Domain
{
    public class CoverShuffleSettingsTests
    {
        [Fact]
        public void Defaults_AreSafeAndNonDestructive()
        {
            var settings = new CoverShuffleSettings();

            Assert.False(settings.Enabled);
            Assert.Equal(TimeSpan.FromHours(24), settings.Interval);
            Assert.Equal(ShuffleMode.Interval, settings.Mode);
            Assert.True(settings.AvoidConsecutiveDuplicates);
            Assert.True(settings.ShuffleOnStartup);
            Assert.False(settings.ShuffleOnGameLaunch);
            Assert.Equal(NotificationPreference.NotifyOnShuffle, settings.NotificationPreference);
        }
    }
}
