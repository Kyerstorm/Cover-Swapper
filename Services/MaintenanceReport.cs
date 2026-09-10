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

        public bool IsEmpty => OrphanedCoverFiles.Count == 0 && InvalidCoverRecords.Count == 0 && CacheFiles.Count == 0;
    }
}
