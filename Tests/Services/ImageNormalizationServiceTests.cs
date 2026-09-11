using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Services;
using Xunit;

namespace PluginCoverShuffle.Tests.Services
{
    public class ImageNormalizationServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly ImageNormalizationService _service = new ImageNormalizationService();

        public ImageNormalizationServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ImageNormalizationServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateImageFile(int width, int height, ImageFormat format, string extension)
        {
            var filePath = Path.Combine(_tempDirectory, Guid.NewGuid().ToString("N") + extension);
            using (var bitmap = new Bitmap(width, height))
            {
                bitmap.Save(filePath, format);
            }
            return filePath;
        }

        [Fact]
        public void Normalize_SmallPngWithinLimits_ReturnsOriginalPathUnchanged()
        {
            var filePath = CreateImageFile(4, 4, ImageFormat.Png, ".png");

            var result = _service.Normalize(filePath);

            Assert.True(result.Success);
            Assert.False(result.WasConverted);
            Assert.Equal(filePath, result.NormalizedFilePath);
        }

        [Fact]
        public void Normalize_OversizedDimensions_DownscalesProportionally()
        {
            var filePath = CreateImageFile(4000, 3000, ImageFormat.Png, ".png");

            var result = _service.Normalize(filePath);

            Assert.True(result.Success);
            Assert.True(result.WasConverted);
            Assert.NotEqual(filePath, result.NormalizedFilePath);

            using (var normalized = Image.FromFile(result.NormalizedFilePath))
            {
                Assert.Equal(CoverImportPolicy.MaxImageDimensionPixels, normalized.Width);
                Assert.Equal((int)Math.Round(3000 * (CoverImportPolicy.MaxImageDimensionPixels / 4000.0)), normalized.Height);
            }

            File.Delete(result.NormalizedFilePath);
        }

        [Fact]
        public void Normalize_ImageWithinDimensionLimit_IsNotDownscaled()
        {
            var filePath = CreateImageFile(200, 300, ImageFormat.Png, ".png");

            var result = _service.Normalize(filePath);

            Assert.True(result.Success);
            Assert.False(result.WasConverted);
        }

        [Fact]
        public void Normalize_CorruptBytesWithWebpExtension_ReturnsFailure()
        {
            var filePath = Path.Combine(_tempDirectory, "corrupt.webp");
            File.WriteAllBytes(filePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

            var result = _service.Normalize(filePath);

            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public void Normalize_MissingFile_ReturnsFailure()
        {
            var result = _service.Normalize(Path.Combine(_tempDirectory, "missing.png"));

            Assert.False(result.Success);
        }

        [Fact]
        public void Normalize_NeverModifiesOrDeletesTheSourceFile()
        {
            var filePath = CreateImageFile(4000, 3000, ImageFormat.Png, ".png");
            var originalBytes = File.ReadAllBytes(filePath);

            var result = _service.Normalize(filePath);

            Assert.True(File.Exists(filePath));
            Assert.Equal(originalBytes, File.ReadAllBytes(filePath));

            if (result.WasConverted)
            {
                File.Delete(result.NormalizedFilePath);
            }
        }
    }
}
