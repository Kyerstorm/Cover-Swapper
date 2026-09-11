using System;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Runs a read-only maintenance scan once when Playnite starts, so users
    /// find out about cover records whose files are missing even if they
    /// never open the Maintenance tool manually. There is no live/periodic
    /// scheduler in this plugin yet, so this follows the same one-shot,
    /// startup-only pattern as <see cref="StartupShuffleService"/>.
    /// </summary>
    public class StartupMaintenanceCheckService
    {
        private readonly MaintenanceService _maintenanceService;
        private readonly CoverShuffleNotificationService _notificationService;
        private readonly Func<CoverShuffleSettings> _globalSettingsProvider;
        private readonly ICoverShuffleLogger _logger;

        public StartupMaintenanceCheckService(
            MaintenanceService maintenanceService,
            CoverShuffleNotificationService notificationService,
            Func<CoverShuffleSettings> globalSettingsProvider,
            ICoverShuffleLogger logger)
        {
            _maintenanceService = maintenanceService ?? throw new ArgumentNullException(nameof(maintenanceService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _globalSettingsProvider = globalSettingsProvider ?? throw new ArgumentNullException(nameof(globalSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RunStartupCheck()
        {
            var report = _maintenanceService.Scan();
            if (report.InvalidCoverRecords.Count == 0)
            {
                _logger.Debug("Startup maintenance check found no covers with missing files.");
                return;
            }

            _logger.Warning($"Startup maintenance check found {report.InvalidCoverRecords.Count} cover(s) with a missing file.");

            var preference = _globalSettingsProvider()?.NotificationPreference ?? NotificationPreference.NotifyOnShuffle;
            _notificationService.NotifyMissingCovers(report.InvalidCoverRecords.Count, preference);
        }
    }
}
