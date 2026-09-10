using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Services
{
    /// <summary>Root object serialized to/from CoverShuffle.json by <see cref="ImportExportService"/>.</summary>
    public class CoverShuffleExport
    {
        /// <summary>
        /// Version of this export file's shape, independent of the
        /// repository's own <see cref="Infrastructure.Persistence.CoverShuffleRepository.CurrentSchemaVersion"/>,
        /// so the export format can evolve on its own schedule.
        /// </summary>
        public int ExportSchemaVersion { get; set; } = 1;

        public DateTime ExportedAtUtc { get; set; }

        public List<GameConfiguration> GameConfigurations { get; set; } = new List<GameConfiguration>();

        public List<ExportedCover> Covers { get; set; } = new List<ExportedCover>();
    }
}
