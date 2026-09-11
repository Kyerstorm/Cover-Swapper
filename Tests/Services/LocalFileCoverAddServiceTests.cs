using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PluginCoverShuffle.Domain;
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
            var importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger(), new ImageNormalizationService());
            _service = new LocalFileCoverAddService(importService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static int _pixelCounter;

        private string CreateValidImageFile()
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                // Vary pixel content so each generated file hashes differently;
                // otherwise every blank bitmap encodes to identical PNG bytes
                // and the duplicate-hash check would reject later "distinct" files.
                var shade = System.Threading.Interlocked.Increment(ref _pixelCounter) % 256;
                bitmap.SetPixel(0, 0, Color.FromArgb(shade, shade, shade));
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        [Fact]
        public void AddFromFile_WithAValidImage_ImportsItAndSucceeds()
        {
            var gameId = Guid.NewGuid();
            var filePath = CreateValidImageFile();

            var result = _service.AddFromFile(gameId, filePath);

            Assert.True(result.IsSuccess);
            Assert.Single(_repository.GetCovers(gameId));
        }

        [Fact]
        public void AddFromFile_WithAMissingFile_ReturnsAnErrorAndImportsNothing()
        {
            var gameId = Guid.NewGuid();
            var missingPath = Path.Combine(_tempDirectory, "does-not-exist.png");

            var result = _service.AddFromFile(gameId, missingPath);

            Assert.False(result.IsSuccess);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void ReplaceFromFile_WithAValidImage_UpdatesTheExistingCover()
        {
            var gameId = Guid.NewGuid();
            _service.AddFromFile(gameId, CreateValidImageFile());
            var existingCoverId = _repository.GetCovers(gameId).Single().CoverId;

            var result = _service.ReplaceFromFile(gameId, existingCoverId, CreateValidImageFile());

            Assert.True(result.IsSuccess);
            Assert.Single(_repository.GetCovers(gameId));
            Assert.Equal(existingCoverId, _repository.GetCovers(gameId).Single().CoverId);
        }

        [Fact]
        public void ReplaceFromFile_WithAMissingFile_ReturnsAnErrorAndLeavesCoverUnchanged()
        {
            var gameId = Guid.NewGuid();
            _service.AddFromFile(gameId, CreateValidImageFile());
            var existingCoverId = _repository.GetCovers(gameId).Single().CoverId;
            var missingPath = Path.Combine(_tempDirectory, "does-not-exist.png");

            var result = _service.ReplaceFromFile(gameId, existingCoverId, missingPath);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void AddManyFromFiles_MultipleValidFiles_ImportsAllAndReturnsSuccessForEach()
        {
            var gameId = Guid.NewGuid();
            var files = new[] { CreateValidImageFile(), CreateValidImageFile(), CreateValidImageFile() };

            var outcomes = _service.AddManyFromFiles(gameId, files);

            Assert.Equal(3, outcomes.Count);
            Assert.All(outcomes, outcome => Assert.True(outcome.Result.IsSuccess));
            Assert.Equal(3, _repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void AddManyFromFiles_OneDuplicateAmongValidFiles_ReturnsDuplicateStatusForThatFileOnly()
        {
            var gameId = Guid.NewGuid();
            var sharedFile = CreateValidImageFile();
            var files = new[] { CreateValidImageFile(), sharedFile, sharedFile };

            var outcomes = _service.AddManyFromFiles(gameId, files);

            Assert.True(outcomes[0].Result.IsSuccess);
            Assert.True(outcomes[1].Result.IsSuccess);
            Assert.False(outcomes[2].Result.IsSuccess);
            Assert.Equal(CoverImportStatus.DuplicateCover, outcomes[2].Result.Status);
            Assert.Equal(2, _repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void AddManyFromFiles_ExceedsRemainingCapacityMidBatch_LaterFilesFailWithCoverLimitExceeded()
        {
            var gameId = Guid.NewGuid();
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame - 2; i++)
            {
                _service.AddFromFile(gameId, CreateValidImageFile());
            }

            var files = new[] { CreateValidImageFile(), CreateValidImageFile(), CreateValidImageFile(), CreateValidImageFile() };
            var outcomes = _service.AddManyFromFiles(gameId, files);

            Assert.True(outcomes[0].Result.IsSuccess);
            Assert.True(outcomes[1].Result.IsSuccess);
            Assert.False(outcomes[2].Result.IsSuccess);
            Assert.Equal(CoverImportStatus.CoverLimitExceeded, outcomes[2].Result.Status);
            Assert.False(outcomes[3].Result.IsSuccess);
            Assert.Equal(CoverImportStatus.CoverLimitExceeded, outcomes[3].Result.Status);
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, _repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void AddManyFromFiles_EmptyList_ReturnsEmptyResult()
        {
            var gameId = Guid.NewGuid();

            var outcomes = _service.AddManyFromFiles(gameId, Enumerable.Empty<string>());

            Assert.Empty(outcomes);
        }

        [Fact]
        public void AddManyFromFiles_InvalidImageAmongValidOnes_ReturnsInvalidImageForThatFileOnlyAndImportsRest()
        {
            var gameId = Guid.NewGuid();
            var corruptFile = Path.Combine(_tempDirectory, "corrupt.png");
            File.WriteAllBytes(corruptFile, new byte[] { 0x00, 0x01, 0x02, 0x03 });
            var files = new[] { CreateValidImageFile(), corruptFile, CreateValidImageFile() };

            var outcomes = _service.AddManyFromFiles(gameId, files);

            Assert.True(outcomes[0].Result.IsSuccess);
            Assert.False(outcomes[1].Result.IsSuccess);
            Assert.Equal(CoverImportStatus.InvalidImage, outcomes[1].Result.Status);
            Assert.True(outcomes[2].Result.IsSuccess);
            Assert.Equal(2, _repository.GetCovers(gameId).Count);
        }
    }
}
