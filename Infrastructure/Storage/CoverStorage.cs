using System;
using System.IO;

namespace PluginCoverShuffle.Infrastructure.Storage
{
    /// <summary>
    /// File-system implementation of <see cref="ICoverStorage"/>. Stored file
    /// names are always derived from <see cref="Guid"/> identifiers rather
    /// than external input, so no additional path sanitization is required
    /// beyond validating the file extension.
    /// </summary>
    public class CoverStorage : ICoverStorage
    {
        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

        private readonly CoverStorageLayout _layout;

        public CoverStorage(CoverStorageLayout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public string SaveCoverFile(Guid gameId, Guid coverId, string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Source cover file was not found.", sourceFilePath);
            }

            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (Array.IndexOf(AllowedExtensions, extension) < 0)
            {
                throw new NotSupportedException($"Cover file extension '{extension}' is not supported.");
            }

            var gameDirectory = _layout.GetGameCoversDirectory(gameId);
            Directory.CreateDirectory(gameDirectory);

            var destinationFileName = coverId.ToString("N") + extension;
            var destinationPath = Path.Combine(gameDirectory, destinationFileName);

            File.Copy(sourceFilePath, destinationPath, overwrite: true);

            return Path.Combine("Covers", gameId.ToString("N"), destinationFileName);
        }

        public void DeleteCoverFile(string relativePath)
        {
            var absolutePath = GetAbsolutePath(relativePath);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }

        public string GetAbsolutePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return Path.Combine(_layout.RootPath, relativePath);
        }

        public bool CoverFileExists(string relativePath)
        {
            return File.Exists(GetAbsolutePath(relativePath));
        }
    }
}
