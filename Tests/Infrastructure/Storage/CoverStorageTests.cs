using System;
using System.IO;
using PluginCoverShuffle.Infrastructure.Storage;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Storage
{
    public class CoverStorageTests : IDisposable
    {
        private readonly string _rootDirectory;
        private readonly string _sourceFilePath;
        private readonly CoverStorageLayout _layout;
        private readonly CoverStorage _storage;

        public CoverStorageTests()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleStorageTests_" + Guid.NewGuid().ToString("N"));
            _layout = new CoverStorageLayout(_rootDirectory);
            _storage = new CoverStorage(_layout);

            Directory.CreateDirectory(_rootDirectory);
            _sourceFilePath = Path.Combine(_rootDirectory, "source-cover.png");
            File.WriteAllBytes(_sourceFilePath, new byte[] { 1, 2, 3, 4 });
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }

        [Fact]
        public void SaveCoverFile_CopiesFileAndReturnsRelativePath()
        {
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();

            var relativePath = _storage.SaveCoverFile(gameId, coverId, _sourceFilePath);

            Assert.True(_storage.CoverFileExists(relativePath));

            var absolutePath = _storage.GetAbsolutePath(relativePath);
            Assert.True(File.Exists(absolutePath));
            Assert.Equal(File.ReadAllBytes(_sourceFilePath), File.ReadAllBytes(absolutePath));
        }

        [Fact]
        public void SaveCoverFile_UnsupportedExtension_Throws()
        {
            var badFilePath = Path.Combine(_rootDirectory, "cover.exe");
            File.WriteAllBytes(badFilePath, new byte[] { 0 });

            Assert.Throws<NotSupportedException>(() =>
                _storage.SaveCoverFile(Guid.NewGuid(), Guid.NewGuid(), badFilePath));
        }

        [Fact]
        public void SaveCoverFile_MissingSourceFile_Throws()
        {
            var missingPath = Path.Combine(_rootDirectory, "does-not-exist.png");

            Assert.Throws<FileNotFoundException>(() =>
                _storage.SaveCoverFile(Guid.NewGuid(), Guid.NewGuid(), missingPath));
        }

        [Fact]
        public void DeleteCoverFile_RemovesStoredFile()
        {
            var gameId = Guid.NewGuid();
            var coverId = Guid.NewGuid();
            var relativePath = _storage.SaveCoverFile(gameId, coverId, _sourceFilePath);

            _storage.DeleteCoverFile(relativePath);

            Assert.False(_storage.CoverFileExists(relativePath));
        }

        [Fact]
        public void DeleteCoverFile_NonExistentFile_DoesNotThrow()
        {
            var relativePath = Path.Combine("Covers", Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") + ".png");
            _storage.DeleteCoverFile(relativePath);
        }

        [Fact]
        public void GetAbsolutePath_WithTraversalSegments_ReturnsNullInsteadOfEscapingRoot()
        {
            var traversalPath = Path.Combine("..", "..", "outside.png");

            Assert.Null(_storage.GetAbsolutePath(traversalPath));
        }

        [Fact]
        public void GetAbsolutePath_WithRootedPath_ReturnsNullInsteadOfEscapingRoot()
        {
            var outsideDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleOutside_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outsideDirectory);
            try
            {
                var rootedPath = Path.Combine(outsideDirectory, "outside.png");

                Assert.Null(_storage.GetAbsolutePath(rootedPath));
            }
            finally
            {
                Directory.Delete(outsideDirectory, recursive: true);
            }
        }

        [Fact]
        public void CoverFileExists_WithTraversalSegments_ReturnsFalseInsteadOfThrowing()
        {
            var traversalPath = Path.Combine("..", "..", "outside.png");

            Assert.False(_storage.CoverFileExists(traversalPath));
        }

        [Fact]
        public void DeleteCoverFile_WithTraversalSegments_DoesNotDeleteAnythingOutsideRoot()
        {
            var outsideDirectory = Path.Combine(Path.GetTempPath(), "CoverShuffleOutside_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outsideDirectory);
            try
            {
                var outsideFile = Path.Combine(outsideDirectory, "outside.png");
                File.WriteAllBytes(outsideFile, new byte[] { 9, 9, 9 });

                var traversalPath = Path.Combine("..", Path.GetFileName(outsideDirectory), "outside.png");

                _storage.DeleteCoverFile(traversalPath);

                Assert.True(File.Exists(outsideFile));
            }
            finally
            {
                Directory.Delete(outsideDirectory, recursive: true);
            }
        }
    }
}
