using System;
using System.IO;
using System.Windows;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class MaintenanceViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverStorageLayout _layout;
        private readonly MaintenanceService _service;
        private readonly FakeDialogsFactory _dialogs = new FakeDialogsFactory();

        public MaintenanceViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "MaintenanceViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            _layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            _layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(_layout);
            _service = new MaintenanceService(_repository, _storage, _layout, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateOrphanFile()
        {
            var gameId = Guid.NewGuid();
            var directory = Path.Combine(_layout.CoversPath, gameId.ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "orphan.png");
            File.WriteAllBytes(path, new byte[] { 1 });
            return path;
        }

        [Fact]
        public void Constructor_ScansImmediately()
        {
            var orphanPath = CreateOrphanFile();

            var viewModel = new MaintenanceViewModel(_service, _dialogs);

            Assert.Contains(orphanPath, viewModel.OrphanedFiles);
            Assert.False(viewModel.IsEmpty);
        }

        [Fact]
        public void DeleteOrphanedFiles_WhenUserDeclines_LeavesFilesInPlace()
        {
            var orphanPath = CreateOrphanFile();
            var viewModel = new MaintenanceViewModel(_service, _dialogs);
            _dialogs.NextMessageBoxResult = MessageBoxResult.No;

            viewModel.DeleteOrphanedFiles();

            Assert.True(File.Exists(orphanPath));
        }

        [Fact]
        public void DeleteOrphanedFiles_WhenUserConfirms_DeletesFiles()
        {
            var orphanPath = CreateOrphanFile();
            var viewModel = new MaintenanceViewModel(_service, _dialogs);
            _dialogs.NextMessageBoxResult = MessageBoxResult.Yes;

            viewModel.DeleteOrphanedFiles();

            Assert.False(File.Exists(orphanPath));
            Assert.Empty(viewModel.OrphanedFiles);
        }

        [Fact]
        public void DeleteOrphanedFiles_AlwaysAsksForConfirmationFirst()
        {
            CreateOrphanFile();
            var viewModel = new MaintenanceViewModel(_service, _dialogs);

            viewModel.DeleteOrphanedFiles();

            Assert.Single(_dialogs.ShownMessages);
        }

        [Fact]
        public void IsEmpty_OnCleanState_IsTrue()
        {
            var viewModel = new MaintenanceViewModel(_service, _dialogs);

            Assert.True(viewModel.IsEmpty);
        }
    }
}
