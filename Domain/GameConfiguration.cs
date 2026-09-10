using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Per-game record tracking whether/how Cover Shuffle is configured for a
    /// specific game. <see cref="SettingsOverride"/> is null when the game
    /// simply follows the global <see cref="CoverShuffleSettings"/> defaults.
    /// </summary>
    public class GameConfiguration
    {
        /// <summary>The Playnite game ID this configuration belongs to.</summary>
        public Guid GameId { get; set; }

        /// <summary>Per-game override of the global defaults, or null to inherit them.</summary>
        public CoverShuffleSettings SettingsOverride { get; set; }
    }
}
