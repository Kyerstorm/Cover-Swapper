using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Services
{
    /// <summary>Read-only findings from <see cref="MaintenanceService.Scan"/>; nothing is deleted until a matching cleanup method is called explicitly.</summary>
    public class MaintenanceReport
    {
        /// <summary>Absolute paths of files under the Covers folder with no matching cover record.</summary>
        public List<string> OrphanedCoverFiles { get; set; } = new List<string>();

        /// <summary>Cover records whose backing image file no longer exists on disk.</summary>
        public List<Cover> InvalidCoverRecords { get; set; } = new List<Cover>();

        /// <summary>Absolute paths of cached (re-downloadable) files under the Cache folder.</summary>
        public List<string> CacheFiles { get; set; } = new List<string>();

        /// <summary>Number of distinct games Cover Shuffle has at least one cover for.</summary>
        public int ManagedGamesCount { get; set; }

        /// <summary>Total cover records across every managed game, including ones whose file is currently missing.</summary>
        public int TotalCoversCount { get; set; }

        /// <summary>Cover records whose backing file is present and intact.</summary>
        public int ValidCoversCount => TotalCoversCount - InvalidCoverRecords.Count;

        /// <summary>Combined size, in bytes, of every file actually present under the Covers folder (valid and orphaned alike).</summary>
        public long CoverStorageSizeBytes { get; set; }

        /// <summary>Combined size, in bytes, of every file under the Cache folder.</summary>
        public long CacheStorageSizeBytes { get; set; }

        /// <summary>
        /// True when there is a genuine problem to fix (a missing cover file
        /// or an orphaned file). Cache files are always safe/expected and are
        /// never counted as a problem on their own.
        /// </summary>
        public bool HasIssues => InvalidCoverRecords.Count > 0 || OrphanedCoverFiles.Count > 0;

        /// <summary>Total number of missing-cover-file and orphaned-file issues combined.</summary>
        public int IssueCount => InvalidCoverRecords.Count + OrphanedCoverFiles.Count;

        public bool IsEmpty => OrphanedCoverFiles.Count == 0 && InvalidCoverRecords.Count == 0 && CacheFiles.Count == 0;
    }
}
