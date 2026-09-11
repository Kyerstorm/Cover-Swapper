using System;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Structured outcome of one <see cref="GameUninstallationService"/> run,
    /// so callers/logs can report exactly what happened rather than a single
    /// pass/fail flag - deletion is a multi-step, partially-recoverable
    /// process and every step's outcome matters.
    /// </summary>
    public class GameUninstallResult
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        /// <summary>True when cleanup completed (including the "nothing to do" case); false only when restoration failed or an unexpected error occurred.</summary>
        public bool Success { get; set; }

        /// <summary>True when Cover Shuffle had never touched this game at all, so there was nothing to restore or clean up.</summary>
        public bool NothingToDo { get; set; }

        public bool OriginalRestored { get; set; }

        /// <summary>How many cover records this game had at the start of cleanup.</summary>
        public int CoverCount { get; set; }

        public int FilesDeleted { get; set; }

        public int FilesFailedToDelete { get; set; }

        /// <summary>True once every plugin-owned record (covers, shuffle state, original artwork, configuration) has been removed.</summary>
        public bool RecordsRemoved { get; set; }

        public bool ShuffleStateRemoved { get; set; }

        /// <summary>User/log-facing explanation, set only when <see cref="Success"/> is false.</summary>
        public string ErrorMessage { get; set; }

        public static GameUninstallResult ForNothingToDo(Guid gameId, string gameName)
        {
            return new GameUninstallResult { GameId = gameId, GameName = gameName, Success = true, NothingToDo = true };
        }

        public static GameUninstallResult RestorationFailed(Guid gameId, string gameName, int coverCount)
        {
            return new GameUninstallResult
            {
                GameId = gameId,
                GameName = gameName,
                Success = false,
                CoverCount = coverCount,
                ErrorMessage =
                    $"Cover Shuffle could not restore the original artwork for {gameName}.{Environment.NewLine}" +
                    $"No Cover Shuffle files were deleted.{Environment.NewLine}" +
                    "The cleanup will be retried."
            };
        }

        public static GameUninstallResult UnexpectedFailure(Guid gameId, string gameName)
        {
            return new GameUninstallResult
            {
                GameId = gameId,
                GameName = gameName,
                Success = false,
                ErrorMessage = $"Cover Shuffle uninstall cleanup for {gameName} failed unexpectedly. See the Cover Shuffle log for details."
            };
        }

        public static GameUninstallResult Completed(Guid gameId, string gameName, bool originalRestored, int coverCount, int filesDeleted, int filesFailedToDelete)
        {
            return new GameUninstallResult
            {
                GameId = gameId,
                GameName = gameName,
                Success = true,
                OriginalRestored = originalRestored,
                CoverCount = coverCount,
                FilesDeleted = filesDeleted,
                FilesFailedToDelete = filesFailedToDelete,
                RecordsRemoved = true,
                ShuffleStateRemoved = true
            };
        }
    }
}
