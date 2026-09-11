using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class StartupMaintenanceCheckServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly MaintenanceService _maintenanceService;
        private readonly FakeNotificationsApi _notifications = new FakeNotificationsApi();
        private readonly CoverShuffleNotificationService _notificationService;
        private CoverShuffleSettings _globalSettings = new CoverShuffleSettings();
        private readonly StartupMaintenanceCheckService _service;

        public StartupMaintenanceCheckServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "StartupMaintenanceCheckServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _maintenanceService = new MaintenanceService(_repository, _storage, layout, new FakeCoverShuffleLogger());
            _notificationService = new CoverShuffleNotificationService(_notifications);
            _service = new StartupMaintenanceCheckService(_maintenanceService, _notificationService, () => _globalSettings, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private Cover AddStoredCover(Guid gameId)
        {
            var sourceFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(sourceFile, new byte[] { 1, 2, 3 });
            var coverId = Guid.NewGuid();
            var relativePath = _storage.SaveCoverFile(gameId, coverId, sourceFile);
            var cover = new Cover
            {
                CoverId = coverId,
                GameId = gameId,
                Source = CoverSource.LocalFile,
                LocalPath = relativePath,
                Hash = coverId.ToString("N"),
                AddedAt = DateTime.UtcNow,
                IsEnabled = true
            };
            _repository.AddCover(cover);
            return cover;
        }

        [Fact]
        public void RunStartupCheck_WithNoMissingCovers_DoesNotNotify()
        {
            AddStoredCover(Guid.NewGuid());

            _service.RunStartupCheck();

            Assert.Empty(_notifications.AddedMessages);
        }

        [Fact]
        public void RunStartupCheck_WithMissingCoverFiles_NotifiesOnceWithTheCount()
        {
            var gameId = Guid.NewGuid();
            var firstMissing = AddStoredCover(gameId);
            var secondMissing = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(firstMissing.LocalPath));
            File.Delete(_storage.GetAbsolutePath(secondMissing.LocalPath));

            _service.RunStartupCheck();

            Assert.Single(_notifications.AddedMessages);
            Assert.Contains("2", _notifications.AddedMessages[0].Text);
        }

        [Fact]
        public void RunStartupCheck_WhenNotificationPreferenceIsSilent_DoesNotNotify()
        {
            _globalSettings = new CoverShuffleSettings { NotificationPreference = NotificationPreference.Silent };
            var gameId = Guid.NewGuid();
            var missing = AddStoredCover(gameId);
            File.Delete(_storage.GetAbsolutePath(missing.LocalPath));

            _service.RunStartupCheck();

            Assert.Empty(_notifications.AddedMessages);
        }
    }
}
