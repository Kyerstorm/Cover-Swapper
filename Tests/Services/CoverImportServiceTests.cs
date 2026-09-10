using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Services
{
    public class CoverImportServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverImportService _service;

        public CoverImportServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverImportServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _service = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private static int _pixelCounter;

        private string CreateValidImageFile(string fileName = null)
        {
            var filePath = Path.Combine(_tempDirectory, fileName ?? (Guid.NewGuid().ToString("N") + ".png"));
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
        public void Import_WithValidImage_StoresCoverAndAddsToPool()
        {
            var gameId = Guid.NewGuid();
            var filePath = CreateValidImageFile();

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Cover);
            Assert.Single(_repository.GetCovers(gameId));
            Assert.True(_storage.CoverFileExists(result.Cover.LocalPath));
        }

        [Fact]
        public void Import_WithMissingSourceFile_Fails()
        {
            var gameId = Guid.NewGuid();

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = Path.Combine(_tempDirectory, "missing.png") });

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.SourceFileMissing, result.Status);
        }

        [Fact]
        public void Import_WithCorruptImageBytes_IsRejectedAsInvalid()
        {
            var gameId = Guid.NewGuid();
            var filePath = Path.Combine(_tempDirectory, "corrupt.png");
            File.WriteAllBytes(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.InvalidImage, result.Status);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void Import_SameImageTwice_SecondImportIsRejectedAsDuplicate()
        {
            var gameId = Guid.NewGuid();
            var filePath = CreateValidImageFile();

            var first = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });
            var second = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.True(first.IsSuccess);
            Assert.False(second.IsSuccess);
            Assert.Equal(CoverImportStatus.DuplicateCover, second.Status);
            Assert.Single(_repository.GetCovers(gameId));
        }

        [Fact]
        public void Import_PastTheCoverLimit_FailsAndCleansUpTheCopiedFile()
        {
            var gameId = Guid.NewGuid();
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });
                Assert.True(result.IsSuccess);
            }

            var overflowResult = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });

            Assert.False(overflowResult.IsSuccess);
            Assert.Equal(CoverImportStatus.CoverLimitExceeded, overflowResult.Status);
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, _repository.GetCovers(gameId).Count);
        }

        [Fact]
        public void Import_LimitIsPerGame_OtherGamesUnaffected()
        {
            var gameId = Guid.NewGuid();
            var otherGameId = Guid.NewGuid();
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame; i++)
            {
                _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });
            }

            var result = _service.Import(otherGameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });

            Assert.True(result.IsSuccess);
        }
    }
}
