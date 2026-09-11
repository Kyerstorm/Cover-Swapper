using System;
using System.IO;
using System.Security.Cryptography;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// The single hashing routine used for cover duplicate detection, so the
    /// import pipeline (<see cref="CoverImportService"/>) and any pre-import
    /// preview UI compute duplicates the same way.
    /// </summary>
    internal static class CoverHashUtility
    {
        public static string ComputeHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
