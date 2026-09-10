using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Services
{
    public class LocalFileCoverAddServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly LocalFileCoverAddService _service;

        public LocalFileCoverAddServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "LocalFileCoverAddServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            var importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger());
            _service = new LocalFileCoverAddService(importService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateValidImageFile()
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        [Fact]
        public void AddFromFile_WithAValidImage_ImportsItAndReturnsNull()
        {
            var gameId = Guid.NewGuid();
            var filePath = CreateValidImageFile();

            var error = _service.AddFromFile(gameId, filePath);

            Assert.Null(error);
            Assert.Single(_repository.GetCovers(gameId));
        }

        [Fact]
        public void AddFromFile_WithAMissingFile_ReturnsAnErrorAndImportsNothing()
        {
            var gameId = Guid.NewGuid();
            var missingPath = Path.Combine(_tempDirectory, "does-not-exist.png");

            var error = _service.AddFromFile(gameId, missingPath);

            Assert.NotNull(error);
            Assert.Empty(_repository.GetCovers(gameId));
        }
    }
}
