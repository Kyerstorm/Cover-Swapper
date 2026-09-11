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
    /// Normalizes a candidate cover image before it is hashed/stored: any
    /// image whose pixel dimensions exceed
    /// <see cref="CoverImportPolicy.MaxImageDimensionPixels"/> is downscaled
    /// proportionally. Never touches or deletes the caller's original source
    /// file; when normalization changes anything, the result is written to a
    /// new temp file that the caller owns.
    /// </summary>
    public class ImageNormalizationService
    {
        public ImageNormalizationResult Normalize(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return ImageNormalizationResult.Failed($"File '{sourceFilePath}' could not be found.");
            }

            try
            {
                using (var source = Image.FromFile(sourceFilePath))
                {
                    var needsResize = source.Width > CoverImportPolicy.MaxImageDimensionPixels
                        || source.Height > CoverImportPolicy.MaxImageDimensionPixels;

                    if (!needsResize)
                    {
                        return ImageNormalizationResult.Unchanged(sourceFilePath);
                    }

                    var longestSide = Math.Max(source.Width, source.Height);
                    var scale = CoverImportPolicy.MaxImageDimensionPixels / (double)longestSide;
                    var targetWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
                    var targetHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

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
                return ImageNormalizationResult.Failed(
                    "This image file could not be read for conversion. Try re-saving it as PNG or JPG.");
            }
        }
    }
}
