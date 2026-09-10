using System;
using Playnite.SDK;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Adapts Playnite's <see cref="ILogger"/> to the plugin's own
    /// <see cref="ICoverShuffleLogger"/> abstraction. This is the only place
    /// in the plugin that should reference Playnite's logging API.
    /// </summary>
    public class PlayniteLoggerAdapter : ICoverShuffleLogger
    {
        private readonly ILogger _logger;

        public PlayniteLoggerAdapter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Debug(string message) => _logger.Debug(message);

        public void Info(string message) => _logger.Info(message);

        public void Warning(string message) => _logger.Warn(message);

        public void Warning(Exception exception, string message) => _logger.Warn(exception, message);

        public void Error(string message) => _logger.Error(message);

        public void Error(Exception exception, string message) => _logger.Error(exception, message);
    }
}
