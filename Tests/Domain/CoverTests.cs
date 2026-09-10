using PluginCoverShuffle.Domain;
using Xunit;

namespace PluginCoverShuffle.Tests.Domain
{
    public class CoverTests
    {
        [Fact]
        public void NewCover_IsEnabledByDefault()
        {
            var cover = new Cover();

            Assert.True(cover.IsEnabled);
        }
    }
}
