using System;
using System.IO;
using PluginCoverShuffle.Infrastructure.Storage;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Storage
{
    public class CoverStorageLayoutTests : IDisposable
    {
        private readonly string _rootDirectory;

        public CoverStorageLayoutTests()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleLayoutTests_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }

        [Fact]
        public void EnsureDirectoriesExist_CreatesAllExpectedSubdirectories()
        {
            var layout = new CoverStorageLayout(_rootDirectory);

            layout.EnsureDirectoriesExist();

            Assert.True(Directory.Exists(layout.DatabasePath));
            Assert.True(Directory.Exists(layout.CoversPath));
            Assert.True(Directory.Exists(layout.CachePath));
            Assert.True(Directory.Exists(layout.LogsPath));
        }

        [Fact]
        public void GetGameCoversDirectory_IsUnderCoversPath()
        {
            var layout = new CoverStorageLayout(_rootDirectory);
            var gameId = Guid.NewGuid();

            var directory = layout.GetGameCoversDirectory(gameId);

            Assert.StartsWith(layout.CoversPath, directory);
            Assert.Contains(gameId.ToString("N"), directory);
        }
    }
}
