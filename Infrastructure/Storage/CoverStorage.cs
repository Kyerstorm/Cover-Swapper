using System;
using System.IO;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Infrastructure.Storage
{
    /// <summary>
    /// File-system implementation of <see cref="ICoverStorage"/>. Stored file
    /// names written by <see cref="SaveCoverFile"/> are always derived from
    /// <see cref="Guid"/> identifiers rather than external input, so no
    /// additional sanitization is required there beyond validating the file
    /// extension. However, <see cref="Domain.Cover.LocalPath"/> values read
    /// back out of the persisted database (or an imported backup) are
    /// external input by the time they reach this class, so every method
    /// that resolves a stored relative path is guarded against escaping the
    /// storage root.
    /// </summary>
    public class CoverStorage : ICoverStorage
    {
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
            if (Array.IndexOf(CoverImportPolicy.AllowedExtensions, extension) < 0)
            {
                throw new NotSupportedException($"Cover file extension '{extension}' is not supported.");
            }

            var gameDirectory = _layout.GetGameCoversDirectory(gameId);
            Directory.CreateDirectory(gameDirectory);

            var destinationFileName = coverId.ToString("N") + extension;
            var destinationPath = Path.Combine(gameDirectory, destinationFileName);
            var tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");

            try
            {
                // Copy into a temp file first, then swap it into place as the
                // final step. A replacement (Services.CoverImportService.ReplaceFile)
                // may target this same destinationPath for an existing,
                // currently-in-use cover; writing directly to it would leave a
                // half-written/corrupt file in place if the copy were
                // interrupted (crash, disk full, killed process). File.Replace
                // performs the swap as a single filesystem operation, so the
                // original file is never observed in a partially-written state.
                File.Copy(sourceFilePath, tempPath, overwrite: true);

                if (File.Exists(destinationPath))
                {
                    File.Replace(tempPath, destinationPath, null);
                }
                else
                {
                    File.Move(tempPath, destinationPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException)
                    {
                        // Best-effort cleanup; a stray temp file is harmless
                        // and must not fail an otherwise-successful save.
                    }
                }
            }

            return Path.Combine("Covers", gameId.ToString("N"), destinationFileName);
        }

        public void DeleteCoverFile(string relativePath)
        {
            var absolutePath = GetAbsolutePath(relativePath);
            if (absolutePath != null && File.Exists(absolutePath))
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

            var rootFull = Path.GetFullPath(_layout.RootPath);
            var candidateFull = Path.GetFullPath(Path.Combine(_layout.RootPath, relativePath));

            var rootWithSeparator = rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? rootFull
                : rootFull + Path.DirectorySeparatorChar;

            if (!candidateFull.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                // A corrupted database entry or a maliciously crafted import
                // file could otherwise smuggle a "../.." path here. Treat it
                // as unresolvable rather than touching a file outside plugin
                // storage; callers already handle a not-found cover file.
                return null;
            }

            return candidateFull;
        }

        public bool CoverFileExists(string relativePath)
        {
            var absolutePath = GetAbsolutePath(relativePath);
            return absolutePath != null && File.Exists(absolutePath);
        }
    }
}
