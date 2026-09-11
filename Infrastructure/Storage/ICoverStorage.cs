using System;

namespace PluginCoverShuffle.Infrastructure.Storage
{
    /// <summary>
    /// Manages physical cover image files within plugin-owned storage.
    /// Does not touch cover metadata; see <see cref="Persistence.ICoverShuffleRepository"/>.
    /// </summary>
    public interface ICoverStorage
    {
        /// <summary>
        /// Copies a source image file into plugin-owned storage for the given
        /// game/cover and returns a path relative to the storage root,
        /// suitable for saving as <see cref="Domain.Cover.LocalPath"/>.
        /// </summary>
        string SaveCoverFile(Guid gameId, Guid coverId, string sourceFilePath);

        /// <summary>Deletes a previously stored cover file, if it exists.</summary>
        void DeleteCoverFile(string relativePath);

        /// <summary>
        /// Resolves a storage-relative path to an absolute file path, or
        /// <c>null</c> if <paramref name="relativePath"/> would resolve
        /// outside plugin-owned storage (e.g. a corrupted or tampered
        /// record containing path-traversal segments).
        /// </summary>
        string GetAbsolutePath(string relativePath);

        /// <summary>Returns whether a stored cover file exists.</summary>
        bool CoverFileExists(string relativePath);
    }
}
