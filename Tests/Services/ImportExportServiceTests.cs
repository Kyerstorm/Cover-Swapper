using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Services
{
    public class ImportExportServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _exportDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly ImportExportService _service;

        public ImportExportServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ImportExportServiceTests_" + Guid.NewGuid().ToString("N"));
            _exportDirectory = Path.Combine(_tempDirectory, "export");
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            _storage = new CoverStorage(layout);
            _service = new ImportExportService(_repository, _storage, new FakeCoverShuffleLogger());
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
        public void Export_WritesCoverShuffleJson_AndCopiesCoverFiles()
        {
            var gameId = Guid.NewGuid();
            _repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId, SettingsOverride = new GameSettingsOverride { Enabled = true } });
            AddStoredCover(gameId);

            var result = _service.Export(_exportDirectory);

            Assert.True(result.Success);
            Assert.True(File.Exists(Path.Combine(_exportDirectory, "CoverShuffle.json")));
            Assert.Single(Directory.GetFiles(Path.Combine(_exportDirectory, "Covers", gameId.ToString("N"))));
        }

        [Fact]
        public void Export_ThenImportIntoAFreshRepository_RestoresConfigurationAndCovers()
        {
            var gameId = Guid.NewGuid();
            _repository.SaveGameConfiguration(new GameConfiguration { GameId = gameId, SettingsOverride = new GameSettingsOverride { Enabled = true } });
            AddStoredCover(gameId);
            _service.Export(_exportDirectory);

            var freshDatabasePath = Path.Combine(_tempDirectory, "fresh.db.json");
            var freshRepository = new CoverShuffleRepository(freshDatabasePath);
            var freshLayout = new CoverStorageLayout(Path.Combine(_tempDirectory, "freshStorage"));
            freshLayout.EnsureDirectoriesExist();
            var freshStorage = new CoverStorage(freshLayout);
            var freshService = new ImportExportService(freshRepository, freshStorage, new FakeCoverShuffleLogger());

            var result = freshService.Import(_exportDirectory);

            Assert.True(result.Success);
            Assert.NotNull(freshRepository.GetGameConfiguration(gameId));
            Assert.Single(freshRepository.GetCovers(gameId));
        }

        [Fact]
        public void Import_RunTwice_DoesNotDuplicateCovers()
        {
            var gameId = Guid.NewGuid();
            AddStoredCover(gameId);
            _service.Export(_exportDirectory);

            var freshDatabasePath = Path.Combine(_tempDirectory, "fresh.db.json");
            var freshRepository = new CoverShuffleRepository(freshDatabasePath);
            var freshLayout = new CoverStorageLayout(Path.Combine(_tempDirectory, "freshStorage"));
            freshLayout.EnsureDirectoriesExist();
            var freshStorage = new CoverStorage(freshLayout);
            var freshService = new ImportExportService(freshRepository, freshStorage, new FakeCoverShuffleLogger());

            freshService.Import(_exportDirectory);
            freshService.Import(_exportDirectory);

            Assert.Single(freshRepository.GetCovers(gameId));
        }

        [Fact]
        public void Import_WithoutCoverShuffleJson_Fails()
        {
            Directory.CreateDirectory(_exportDirectory);

            var result = _service.Import(_exportDirectory);

            Assert.False(result.Success);
        }

        [Fact]
        public void Import_WithCorruptJson_FailsGracefully()
        {
            Directory.CreateDirectory(_exportDirectory);
            File.WriteAllText(Path.Combine(_exportDirectory, "CoverShuffle.json"), "not valid json {{{");

            var result = _service.Import(_exportDirectory);

            Assert.False(result.Success);
        }

        [Fact]
        public void Import_WithPathTraversalInRelativeFilePath_SkipsCoverInsteadOfReadingOutsideFile()
        {
            Directory.CreateDirectory(_exportDirectory);
            var outsideFile = Path.Combine(_tempDirectory, "outside.png");
            File.WriteAllBytes(outsideFile, new byte[] { 9, 9, 9 });

            var gameId = Guid.NewGuid();
            var export = new CoverShuffleExport
            {
                GameConfigurations = new List<GameConfiguration>(),
                Covers = new List<ExportedCover>
                {
                    new ExportedCover
                    {
                        Cover = new Cover
                        {
                            CoverId = Guid.NewGuid(),
                            GameId = gameId,
                            Source = CoverSource.LocalFile,
                            Hash = "traversal-hash",
                            AddedAt = DateTime.UtcNow,
                            IsEnabled = true
                        },
                        // Attempts to escape the export folder and read the
                        // file created above instead of a legitimate export.
                        RelativeFilePath = Path.Combine("..", "outside.png")
                    }
                }
            };
            File.WriteAllText(Path.Combine(_exportDirectory, "CoverShuffle.json"), JsonConvert.SerializeObject(export, Formatting.Indented));

            var result = _service.Import(_exportDirectory);

            Assert.True(result.Success);
            Assert.Empty(_repository.GetCovers(gameId));
        }

        [Fact]
        public void Export_WithNoDestination_Fails()
        {
            var result = _service.Export(null);

            Assert.False(result.Success);
        }
    }
}
