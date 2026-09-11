using System;
using System.Collections.Generic;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>No-op logger fake used to satisfy dependencies in tests. Records every call so tests can assert a failure was actually logged.</summary>
    public class FakeCoverShuffleLogger : ICoverShuffleLogger
    {
        public List<string> DebugMessages { get; } = new List<string>();

        public List<string> InfoMessages { get; } = new List<string>();

        public List<string> WarningMessages { get; } = new List<string>();

        public List<string> ErrorMessages { get; } = new List<string>();

        public void Debug(string message) => DebugMessages.Add(message);

        public void Info(string message) => InfoMessages.Add(message);

        public void Warning(string message) => WarningMessages.Add(message);

        public void Warning(Exception exception, string message) => WarningMessages.Add(message);

        public void Error(string message) => ErrorMessages.Add(message);

        public void Error(Exception exception, string message) => ErrorMessages.Add(message);
    }
}
