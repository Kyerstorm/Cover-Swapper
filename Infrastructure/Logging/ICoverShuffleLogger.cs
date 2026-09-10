using System;

namespace PluginCoverShuffle.Infrastructure.Logging
{
    /// <summary>
    /// Logging abstraction used throughout the plugin so that core/domain and
    /// application code does not depend directly on Playnite's logging API.
    /// </summary>
    public interface ICoverShuffleLogger
    {
        void Debug(string message);

        void Info(string message);

        void Warning(string message);

        void Warning(Exception exception, string message);

        void Error(string message);

        void Error(Exception exception, string message);
    }
}
