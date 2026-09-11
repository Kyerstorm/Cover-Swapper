using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Normalizes a candidate cover image before it is hashed/stored:
    /// WEBP files are re-encoded as PNG (WPF/GDI+ WEBP decoding is not
    /// guaranteed on every Windows install, so storing WEBP as-is risks a
    /// cover that silently fails to render later), and any image whose
    /// pixel dimensions exceed <see cref="CoverImportPolicy.MaxImageDimensionPixels"/>
    /// is downscaled proportionally. Never touches or deletes the caller's
    /// original source file; when normalization changes anything, the
    /// result is written to a new temp file that the caller owns.
    /// </summary>
    public class ImageNormalizationService
    {
        public ImageNormalizationResult Normalize(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return ImageNormalizationResult.Failed($"File '{sourceFilePath}' could not be found.");
            }

            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            var needsFormatConversion = extension == ".webp";

            try
            {
                using (var source = Image.FromFile(sourceFilePath))
                {
                    var needsResize = source.Width > CoverImportPolicy.MaxImageDimensionPixels
                        || source.Height > CoverImportPolicy.MaxImageDimensionPixels;

                    if (!needsFormatConversion && !needsResize)
                    {
                        return ImageNormalizationResult.Unchanged(sourceFilePath);
                    }

                    int targetWidth = source.Width;
                    int targetHeight = source.Height;
                    if (needsResize)
                    {
                        var longestSide = Math.Max(source.Width, source.Height);
                        var scale = CoverImportPolicy.MaxImageDimensionPixels / (double)longestSide;
                        targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
                        targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
                    }

                    using (var resized = new Bitmap(targetWidth, targetHeight))
                    {
                        using (var graphics = Graphics.FromImage(resized))
                        {
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = SmoothingMode.HighQuality;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);
                        }

                        var tempFilePath = Path.Combine(Path.GetTempPath(), $"covershuffle_norm_{Guid.NewGuid():N}.png");
                        resized.Save(tempFilePath, ImageFormat.Png);
                        return ImageNormalizationResult.Converted(tempFilePath);
                    }
                }
            }
            catch (Exception ex) when (ex is OutOfMemoryException || ex is ArgumentException || ex is IOException || ex is ExternalException)
            {
                // GDI+ throws OutOfMemoryException for unrecognized/corrupt
                // image data (including WEBP files when the OS lacks a WEBP
                // codec) rather than a more descriptive exception type.
                var friendlyFormat = extension == ".webp" ? "WEBP" : "image";
                return ImageNormalizationResult.Failed(
                    $"This {friendlyFormat} file could not be read for conversion. Try re-saving it as PNG or JPG.");
            }
        }
    }
}
