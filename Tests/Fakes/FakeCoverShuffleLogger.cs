using System;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>No-op logger fake used to satisfy dependencies in tests.</summary>
    public class FakeCoverShuffleLogger : ICoverShuffleLogger
    {
        public void Debug(string message) { }

        public void Info(string message) { }

        public void Warning(string message) { }

        public void Warning(Exception exception, string message) { }

        public void Error(string message) { }

        public void Error(Exception exception, string message) { }
    }
}
