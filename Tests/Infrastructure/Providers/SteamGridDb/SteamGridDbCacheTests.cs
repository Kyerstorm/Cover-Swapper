using System;
using System.IO;
using PluginCoverShuffle.Infrastructure.Providers.SteamGridDb;
using Xunit;

namespace PluginCoverShuffle.Tests.Infrastructure.Providers.SteamGridDb
{
    public class SteamGridDbCacheTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly SteamGridDbCache _cache;

        public SteamGridDbCacheTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "SteamGridDbCacheTests_" + Guid.NewGuid().ToString("N"));
            _cache = new SteamGridDbCache(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void TryGetCachedFile_WhenNothingCached_ReturnsFalse()
        {
            var found = _cache.TryGetCachedFile(123, out var filePath);

            Assert.False(found);
            Assert.Null(filePath);
        }

        [Fact]
        public void SaveToCache_ThenTryGetCachedFile_RoundTrips()
        {
            var bytes = new byte[] { 1, 2, 3, 4 };

            var savedPath = _cache.SaveToCache(123, bytes, "https://cdn.example/full.png");

            Assert.True(File.Exists(savedPath));
            Assert.Equal(bytes, File.ReadAllBytes(savedPath));

            var found = _cache.TryGetCachedFile(123, out var cachedPath);
            Assert.True(found);
            Assert.Equal(savedPath, cachedPath);
        }

        [Fact]
        public void SaveToCache_WithUnrecognizedExtension_DefaultsToPng()
        {
            var savedPath = _cache.SaveToCache(456, new byte[] { 1 }, "https://cdn.example/full.exe");

            Assert.EndsWith(".png", savedPath);
        }

        [Fact]
        public void SaveToCache_DifferentGridIds_DoNotCollide()
        {
            var first = _cache.SaveToCache(1, new byte[] { 1 }, "https://cdn.example/a.png");
            var second = _cache.SaveToCache(2, new byte[] { 2 }, "https://cdn.example/b.png");

            Assert.NotEqual(first, second);
            Assert.True(_cache.TryGetCachedFile(1, out _));
            Assert.True(_cache.TryGetCachedFile(2, out _));
        }
    }
}
