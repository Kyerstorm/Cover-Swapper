using System;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Shuffling;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class BulkConfigurationServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ICoverShuffleRepository _repository;
        private readonly PlayniteCoverService _coverService;
        private readonly BulkConfigurationService _service;

        public BulkConfigurationServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "BulkConfigurationServiceTests_" + Guid.NewGuid().ToString("N"));
            var databaseFilePath = Path.Combine(_tempDirectory, "coverShuffle.db.json");
            _repository = new CoverShuffleRepository(databaseFilePath);
            var layout = new CoverStorageLayout(Path.Combine(_tempDirectory, "storage"));
            layout.EnsureDirectoriesExist();
            var storage = new CoverStorage(layout);
            _coverService = new PlayniteCoverService(_repository, new FakePlayniteGameService(), storage, () => new CoverShuffleSettings(), new FakeCoverShuffleLogger(), new ShuffleEngine(new FakeShuffleRandomizer()));
            _service = new BulkConfigurationService(_coverService, new FakeCoverShuffleLogger());
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void EnableAll_EnablesEveryListedGame()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();

            _service.EnableAll(new[] { first, second });

            Assert.True(_coverService.IsEnabled(first));
            Assert.True(_coverService.IsEnabled(second));
        }

        [Fact]
        public void DisableAll_DisablesEveryListedGame()
        {
            var gameId = Guid.NewGuid();
            _coverService.EnableCoverShuffle(gameId);

            _service.DisableAll(new[] { gameId });

            Assert.False(_coverService.IsEnabled(gameId));
        }

        [Fact]
        public void SetIntervalForAll_SavesTheIntervalAsAPerGameOverride()
        {
            var gameId = Guid.NewGuid();

            _service.SetIntervalForAll(new[] { gameId }, TimeSpan.FromHours(3));

            var configuration = _repository.GetGameConfiguration(gameId);
            Assert.NotNull(configuration?.SettingsOverride);
            Assert.Equal(TimeSpan.FromHours(3), configuration.SettingsOverride.Interval);
        }

        [Fact]
        public void EnableAll_WithEmptyList_DoesNothing()
        {
            var exception = Record.Exception(() => _service.EnableAll(new Guid[0]));

            Assert.Null(exception);
        }
    }
}
