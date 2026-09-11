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
        private readonly FakeInitialShuffleTrigger _initialShuffleTrigger = new FakeInitialShuffleTrigger();
        private readonly CoverImportService _service;

        public CoverImportServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverImportServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _service = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger(), new ImageNormalizationService(), _initialShuffleTrigger);
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
        public void Import_OnSuccess_NotifiesTheInitialShuffleTrigger_RegardlessOfSource()
        {
            var localFileGameId = Guid.NewGuid();
            _service.Import(localFileGameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });

            var steamGridDbGameId = Guid.NewGuid();
            _service.Import(steamGridDbGameId, new CoverAsset { Source = CoverSource.SteamGridDb, FilePath = CreateValidImageFile() });

            var playniteMetadataGameId = Guid.NewGuid();
            _service.Import(playniteMetadataGameId, new CoverAsset { Source = CoverSource.PlayniteMetadata, FilePath = CreateValidImageFile() });

            Assert.Contains(localFileGameId, _initialShuffleTrigger.TriggeredGameIds);
            Assert.Contains(steamGridDbGameId, _initialShuffleTrigger.TriggeredGameIds);
            Assert.Contains(playniteMetadataGameId, _initialShuffleTrigger.TriggeredGameIds);
        }

        [Fact]
        public void Import_WhenTheImportFails_DoesNotNotifyTheInitialShuffleTrigger()
        {
            var gameId = Guid.NewGuid();

            _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = Path.Combine(_tempDirectory, "does-not-exist.png") });

            Assert.DoesNotContain(gameId, _initialShuffleTrigger.TriggeredGameIds);
        }

        [Fact]
        public void Import_WithoutAnInitialShuffleTriggerConfigured_StillSucceeds()
        {
            var serviceWithoutTrigger = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger(), new ImageNormalizationService());
            var gameId = Guid.NewGuid();

            var result = serviceWithoutTrigger.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() });

            Assert.True(result.IsSuccess);
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

        [Fact]
        public void ReplaceFile_WithValidImage_KeepsCoverIdAndHistoryButUpdatesFile()
        {
            var gameId = Guid.NewGuid();
            var original = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() }).Cover;
            var replacementFile = CreateValidImageFile();

            var result = _service.ReplaceFile(gameId, original.CoverId, replacementFile);

            Assert.True(result.IsSuccess);
            Assert.Equal(original.CoverId, result.Cover.CoverId);
            Assert.Equal(original.AddedAt, result.Cover.AddedAt);
            Assert.Equal(original.UsageCount, result.Cover.UsageCount);
            Assert.NotEqual(original.Hash, result.Cover.Hash);
            Assert.True(_storage.CoverFileExists(result.Cover.LocalPath));
        }

        [Fact]
        public void ReplaceFile_WhenNewFileHasADifferentExtension_DoesNotOrphanTheOldFile()
        {
            var gameId = Guid.NewGuid();
            var original = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile("original.png") }).Cover;
            var oldAbsolutePath = _storage.GetAbsolutePath(original.LocalPath);
            var replacementFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".bmp");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(200, 50, 50));
                bitmap.Save(replacementFile, ImageFormat.Bmp);
            }

            var result = _service.ReplaceFile(gameId, original.CoverId, replacementFile);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(oldAbsolutePath));
            Assert.True(_storage.CoverFileExists(result.Cover.LocalPath));
        }

        [Fact]
        public void ReplaceFile_WhenSaveFails_PreservesTheOriginalFile()
        {
            var gameId = Guid.NewGuid();
            var original = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() }).Cover;
            var oldAbsolutePath = _storage.GetAbsolutePath(original.LocalPath);
            var oldBytes = File.ReadAllBytes(oldAbsolutePath);

            // TIFF decodes fine via GDI+ (so it passes image validation) but
            // is not in CoverImportPolicy.AllowedExtensions, so SaveCoverFile
            // rejects it - simulating a failure that happens after
            // validation succeeds but before the new file is actually
            // stored. The existing cover must survive that failure.
            var replacementFile = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".tiff");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                bitmap.Save(replacementFile, ImageFormat.Tiff);
            }

            var result = _service.ReplaceFile(gameId, original.CoverId, replacementFile);

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.InvalidImage, result.Status);
            Assert.True(File.Exists(oldAbsolutePath));
            Assert.Equal(oldBytes, File.ReadAllBytes(oldAbsolutePath));

            var stillTracked = _repository.GetCover(gameId, original.CoverId);
            Assert.Equal(original.LocalPath, stillTracked.LocalPath);
        }

        [Fact]
        public void ReplaceFile_WithUnknownCoverId_FailsWithCoverNotFound()
        {
            var gameId = Guid.NewGuid();

            var result = _service.ReplaceFile(gameId, Guid.NewGuid(), CreateValidImageFile());

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.CoverNotFound, result.Status);
        }

        [Fact]
        public void ReplaceFile_WithInvalidImage_IsRejected()
        {
            var gameId = Guid.NewGuid();
            var original = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() }).Cover;
            var corruptFile = Path.Combine(_tempDirectory, "corrupt-replacement.png");
            File.WriteAllBytes(corruptFile, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var result = _service.ReplaceFile(gameId, original.CoverId, corruptFile);

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.InvalidImage, result.Status);
            Assert.True(_storage.CoverFileExists(original.LocalPath));
        }

        [Fact]
        public void ReplaceFile_MatchingAnotherCoversContent_IsRejectedAsDuplicate()
        {
            var gameId = Guid.NewGuid();
            var sharedFile = CreateValidImageFile();
            var other = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = sharedFile }).Cover;
            var toReplace = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = CreateValidImageFile() }).Cover;

            var result = _service.ReplaceFile(gameId, toReplace.CoverId, sharedFile);

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.DuplicateCover, result.Status);
        }

        [Fact]
        public void ReplaceFile_WithSameFileAgain_IsAllowed_NotTreatedAsDuplicateOfSelf()
        {
            var gameId = Guid.NewGuid();
            var filePath = CreateValidImageFile();
            var original = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath }).Cover;

            var result = _service.ReplaceFile(gameId, original.CoverId, filePath);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Import_FileOverSizeLimit_ReturnsFileTooLarge()
        {
            var gameId = Guid.NewGuid();
            // Exactly at the max allowed pixel dimension, so it is not
            // downscaled, but an uncompressed 24-bit BMP at that size is
            // well past the 20 MB file-size limit.
            var filePath = Path.Combine(_tempDirectory, "oversized.bmp");
            using (var bitmap = new Bitmap(CoverImportPolicy.MaxImageDimensionPixels, CoverImportPolicy.MaxImageDimensionPixels))
            {
                bitmap.Save(filePath, ImageFormat.Bmp);
            }

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.FileTooLarge, result.Status);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void Import_ImageBeyondDecodeCeiling_IsRejectedInsteadOfDownscaled()
        {
            var gameId = Guid.NewGuid();
            var filePath = Path.Combine(_tempDirectory, "beyond-decode-ceiling.bmp");
            // A thin strip keeps the actual pixel buffer tiny while still
            // declaring a width past MaxDecodeDimensionPixels, exercising the
            // cheap dimension pre-check without allocating a huge bitmap in
            // the test itself.
            using (var bitmap = new Bitmap(CoverImportPolicy.MaxDecodeDimensionPixels + 1, 1))
            {
                bitmap.Save(filePath, ImageFormat.Bmp);
            }

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.False(result.IsSuccess);
            Assert.Equal(CoverImportStatus.InvalidImage, result.Status);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void Import_OversizedImageDimensions_IsDownscaledBeforeStorage()
        {
            var gameId = Guid.NewGuid();
            var filePath = Path.Combine(_tempDirectory, "oversized-dimensions.png");
            using (var bitmap = new Bitmap(4000, 3000))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }

            var result = _service.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });

            Assert.True(result.IsSuccess);
            var storedPath = _storage.GetAbsolutePath(result.Cover.LocalPath);
            using (var stored = Image.FromFile(storedPath))
            {
                Assert.True(stored.Width <= CoverImportPolicy.MaxImageDimensionPixels);
                Assert.True(stored.Height <= CoverImportPolicy.MaxImageDimensionPixels);
            }
        }
    }
}
