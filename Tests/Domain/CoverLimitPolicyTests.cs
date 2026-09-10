using PluginCoverShuffle.Domain;
using Xunit;

namespace PluginCoverShuffle.Tests.Domain
{
    public class CoverLimitPolicyTests
    {
        [Fact]
        public void MaxCoversPerGame_IsTen()
        {
            Assert.Equal(10, CoverLimitPolicy.MaxCoversPerGame);
        }
    }
}
