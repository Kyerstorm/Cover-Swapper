using System;
using System.IO;

namespace PluginCoverShuffle.Infrastructure.Storage
{
    /// <summary>
    /// Computes the plugin-owned storage directories, rooted under a base
    /// path supplied by the host. Playnite already scopes
    /// <c>GetPluginUserDataPath()</c> to this plugin, so that path is used
    /// directly as the root rather than nesting a redundant folder under it.
    /// </summary>
    public class CoverStorageLayout
    {
        public CoverStorageLayout(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("Root path must be provided.", nameof(rootPath));
            }

            RootPath = rootPath;
            DatabasePath = Path.Combine(rootPath, "Database");
            CoversPath = Path.Combine(rootPath, "Covers");
            CachePath = Path.Combine(rootPath, "Cache");
            LogsPath = Path.Combine(rootPath, "Logs");
        }

        public string RootPath { get; }

        public string DatabasePath { get; }

        public string CoversPath { get; }

        public string CachePath { get; }

        public string LogsPath { get; }

        public string DatabaseFilePath => Path.Combine(DatabasePath, "coverShuffle.db.json");

        public string GetGameCoversDirectory(Guid gameId) => Path.Combine(CoversPath, gameId.ToString("N"));

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(DatabasePath);
            Directory.CreateDirectory(CoversPath);
            Directory.CreateDirectory(CachePath);
            Directory.CreateDirectory(LogsPath);
        }
    }
}
