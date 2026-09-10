using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Infrastructure.Persistence
{
    /// <summary>
    /// Root document persisted to disk. Kept separate from the domain model
    /// so the on-disk shape (including <see cref="SchemaVersion"/>) can evolve
    /// independently of in-memory types.
    /// </summary>
    internal class PersistedDatabase
    {
        public int SchemaVersion { get; set; } = CoverShuffleRepository.CurrentSchemaVersion;

        public List<GameConfiguration> GameConfigurations { get; set; } = new List<GameConfiguration>();

        public List<Cover> Covers { get; set; } = new List<Cover>();

        public List<ShuffleState> ShuffleStates { get; set; } = new List<ShuffleState>();

        public List<OriginalArtworkInfo> OriginalArtworkRecords { get; set; } = new List<OriginalArtworkInfo>();
    }
}
