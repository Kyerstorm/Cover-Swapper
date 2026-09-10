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
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class CoverManagementViewModelTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly CoverImportService _importService;

        public CoverManagementViewModelTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "CoverManagementViewModelTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _importService = new CoverImportService(_repository, _storage, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private Cover ImportCover(Guid gameId)
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + ".png");
            using (var bitmap = new Bitmap(4, 4))
            {
                bitmap.Save(filePath, ImageFormat.Png);
            }

            var result = _importService.Import(gameId, new CoverAsset { Source = CoverSource.LocalFile, FilePath = filePath });
            Assert.True(result.IsSuccess);
            return result.Cover;
        }

        [Fact]
        public void Constructor_LoadsExistingCoversForTheGame()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);

            var viewModel = new CoverManagementViewModel(gameId, _repository, _storage);

            Assert.Single(viewModel.Covers);
            Assert.Equal(cover.CoverId, viewModel.Covers[0].CoverId);
            Assert.Equal(_storage.GetAbsolutePath(cover.LocalPath), viewModel.Covers[0].AbsoluteImagePath);
        }

        [Fact]
        public void Remove_TakesCoverOutOfThePool_ButLeavesTheFileOnDisk()
        {
            var gameId = Guid.NewGuid();
            var cover = ImportCover(gameId);
            var viewModel = new CoverManagementViewModel(gameId, _repository, _storage);

            viewModel.Remove(cover.CoverId);

            Assert.Empty(viewModel.Covers);
            Assert.Empty(_repository.GetCovers(gameId));
            Assert.True(_storage.CoverFileExists(cover.LocalPath));
        }
    }
}
