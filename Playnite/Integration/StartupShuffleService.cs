using System;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Runs the due-shuffle check across every game Cover Shuffle knows
    /// about when Playnite starts. Only reads games with a saved
    /// <see cref="GameConfiguration"/> from the plugin's own repository —
    /// never scans Playnite's full library — and is meant to be invoked off
    /// the UI thread so it never delays Playnite's startup.
    /// </summary>
    public class StartupShuffleService
    {
        private readonly ICoverShuffleRepository _repository;
        private readonly ScheduledShuffleService _scheduledShuffleService;
        private readonly Func<CoverShuffleSettings> _globalSettingsProvider;
        private readonly ICoverShuffleLogger _logger;

        public StartupShuffleService(
            ICoverShuffleRepository repository,
            ScheduledShuffleService scheduledShuffleService,
            Func<CoverShuffleSettings> globalSettingsProvider,
            ICoverShuffleLogger logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _scheduledShuffleService = scheduledShuffleService ?? throw new ArgumentNullException(nameof(scheduledShuffleService));
            _globalSettingsProvider = globalSettingsProvider ?? throw new ArgumentNullException(nameof(globalSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RunDueShuffles()
        {
            if (_globalSettingsProvider()?.ShuffleOnStartup == false)
            {
                _logger.Debug("Shuffle-on-startup is disabled; skipping startup shuffle check.");
                return;
            }

            var configurations = _repository.GetAllGameConfigurations();
            _logger.Debug($"Checking {configurations.Count} known game(s) for a due shuffle at startup.");

            foreach (var configuration in configurations)
            {
                try
                {
                    _scheduledShuffleService.ShuffleIfDue(configuration.GameId);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Startup shuffle check failed for game '{configuration.GameId}'.");
                }
            }
        }
    }
}
