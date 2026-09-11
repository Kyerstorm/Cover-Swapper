using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class AddLocalCoversViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly CoverImportService _importService;
        private readonly LocalFileCoverAddService _addService;
        private readonly Guid _gameId = Guid.NewGuid();
        private static int _pixelCounter;

        public AddLocalCoversViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AddLocalCoversViewModelTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, storage, new FakeCoverShuffleLogger(), new ImageNormalizationService());
            _addService = new LocalFileCoverAddService(_importService);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private AddLocalCoversViewModel CreateViewModel()
        {
            return new AddLocalCoversViewModel(_gameId, _repository, _addService, new FakeCoverShuffleLogger());
        }

        private string CreateValidImageFile(string fileName = null)
        {
            var filePath = Path.Combine(_tempDirectory, fileName ?? (Guid.NewGuid().ToString("N") + ".png"));
            using (var bitmap = new Bitmap(4, 4))
            {
                var shade = System.Threading.Interlocked.Increment(ref _pixelCounter) % 256;
                bitmap.SetPixel(0, 0, Color.FromArgb(shade, shade, shade));
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath;
        }

        [Fact]
        public void AddCandidateFiles_ValidImage_SetsStatusValid()
        {
            var viewModel = CreateViewModel();

            viewModel.AddCandidateFiles(new[] { CreateValidImageFile() });

            var candidate = Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.Valid, candidate.Status);
            Assert.True(candidate.IsSelected);
        }

        [Fact]
        public void AddCandidateFiles_UnsupportedExtension_SetsStatusInvalid()
        {
            var viewModel = CreateViewModel();
            var filePath = Path.Combine(_tempDirectory, "not-an-image.txt");
            File.WriteAllText(filePath, "hello");

            viewModel.AddCandidateFiles(new[] { filePath });

            var candidate = Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.Invalid, candidate.Status);
        }

        [Fact]
        public void AddCandidateFiles_CorruptImageBytes_SetsStatusInvalid()
        {
            var viewModel = CreateViewModel();
            var filePath = Path.Combine(_tempDirectory, "corrupt.png");
            File.WriteAllBytes(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            viewModel.AddCandidateFiles(new[] { filePath });

            var candidate = Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.Invalid, candidate.Status);
        }

        [Fact]
        public void AddCandidateFiles_OversizedFile_SetsStatusTooLarge()
        {
            var viewModel = CreateViewModel();
            var filePath = Path.Combine(_tempDirectory, "oversized.bmp");
            using (var bitmap = new Bitmap(CoverImportPolicy.MaxImageDimensionPixels, CoverImportPolicy.MaxImageDimensionPixels))
            {
                bitmap.Save(filePath, ImageFormat.Bmp);
            }

            viewModel.AddCandidateFiles(new[] { filePath });

            var candidate = Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.TooLarge, candidate.Status);
        }

        [Fact]
        public void AddCandidateFiles_DuplicateOfExistingCover_SetsStatusDuplicateOfExisting()
        {
            var filePath = CreateValidImageFile();
            var importResult = _addService.AddFromFile(_gameId, filePath);
            Assert.True(importResult.IsSuccess);

            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { filePath });

            var candidate = Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.DuplicateOfExisting, candidate.Status);
        }

        [Fact]
        public void AddCandidateFiles_TwoIdenticalFilesInSameBatch_SecondIsFlaggedDuplicateInBatch()
        {
            var viewModel = CreateViewModel();
            var sharedFile = CreateValidImageFile();

            viewModel.AddCandidateFiles(new[] { sharedFile });
            // A second, distinct path with identical bytes still hashes the same.
            var copyPath = Path.Combine(_tempDirectory, "copy.png");
            File.Copy(sharedFile, copyPath);
            viewModel.AddCandidateFiles(new[] { copyPath });

            Assert.Equal(LocalFileCandidateStatus.Valid, viewModel.Candidates[0].Status);
            Assert.Equal(LocalFileCandidateStatus.DuplicateInBatch, viewModel.Candidates[1].Status);
        }

        [Fact]
        public void AddCandidateFiles_MoreFilesThanRemainingCapacity_ExcessFlaggedWillExceedLimit()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame - 1; i++)
            {
                _addService.AddFromFile(_gameId, CreateValidImageFile());
            }

            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { CreateValidImageFile(), CreateValidImageFile() });

            Assert.Equal(LocalFileCandidateStatus.Valid, viewModel.Candidates[0].Status);
            Assert.Equal(LocalFileCandidateStatus.WillExceedLimit, viewModel.Candidates[1].Status);
        }

        [Fact]
        public void RemoveCandidate_RemovesFromListAndRecomputesCapacityFlags()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame - 1; i++)
            {
                _addService.AddFromFile(_gameId, CreateValidImageFile());
            }

            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { CreateValidImageFile(), CreateValidImageFile() });
            Assert.Equal(LocalFileCandidateStatus.WillExceedLimit, viewModel.Candidates[1].Status);

            viewModel.RemoveCandidate(viewModel.Candidates[0]);

            Assert.Single(viewModel.Candidates);
            Assert.Equal(LocalFileCandidateStatus.Valid, viewModel.Candidates[0].Status);
        }

        [Fact]
        public async Task ImportSelectedAsync_CommitsOnlyValidCandidatesAndUpdatesTheirStatus()
        {
            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { CreateValidImageFile(), CreateValidImageFile() });

            await viewModel.ImportSelectedAsync();

            Assert.All(viewModel.Candidates, c => Assert.Equal(LocalFileCandidateStatus.Imported, c.Status));
            Assert.Equal(2, _repository.GetCovers(_gameId).Count);
        }

        [Fact]
        public async Task ImportSelectedAsync_OnlyOneSlotRemaining_ImportsTheFirstAndLeavesTheOverflowCandidateUnimported()
        {
            for (var i = 0; i < CoverLimitPolicy.MaxCoversPerGame - 1; i++)
            {
                _addService.AddFromFile(_gameId, CreateValidImageFile());
            }

            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { CreateValidImageFile(), CreateValidImageFile() });
            // The preview already flagged the second file as WillExceedLimit
            // (a hint, not a hard block); ImportSelectedAsync only commits
            // candidates still marked Valid, so the flagged one is left alone
            // rather than being attempted and failing.
            Assert.Equal(LocalFileCandidateStatus.Valid, viewModel.Candidates[0].Status);
            Assert.Equal(LocalFileCandidateStatus.WillExceedLimit, viewModel.Candidates[1].Status);

            await viewModel.ImportSelectedAsync();

            Assert.Equal(LocalFileCandidateStatus.Imported, viewModel.Candidates[0].Status);
            Assert.Equal(LocalFileCandidateStatus.WillExceedLimit, viewModel.Candidates[1].Status);
            Assert.Equal(CoverLimitPolicy.MaxCoversPerGame, _repository.GetCovers(_gameId).Count);
        }

        [Fact]
        public void CanImportSelected_FalseWhenNoValidSelectedCandidates()
        {
            var viewModel = CreateViewModel();

            Assert.False(viewModel.CanImportSelected);

            var filePath = Path.Combine(_tempDirectory, "not-an-image.txt");
            File.WriteAllText(filePath, "hello");
            viewModel.AddCandidateFiles(new[] { filePath });

            Assert.False(viewModel.CanImportSelected);
        }

        [Fact]
        public void CanImportSelected_TrueWhenAValidCandidateIsSelected()
        {
            var viewModel = CreateViewModel();
            viewModel.AddCandidateFiles(new[] { CreateValidImageFile() });

            Assert.True(viewModel.CanImportSelected);
        }
    }
}
