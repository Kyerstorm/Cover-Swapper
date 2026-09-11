using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Per-game record tracking whether/how Cover Shuffle is configured for a
    /// specific game. <see cref="SettingsOverride"/> is null when the game
    /// follows every global <see cref="CoverShuffleSettings"/> default; when
    /// present, each of its fields independently inherits (null) or overrides
    /// the corresponding global value.
    /// </summary>
    public class GameConfiguration
    {
        public GameConfiguration()
        {
        }

        /// <summary>Creates an independent copy of <paramref name="source"/>.</summary>
        public GameConfiguration(GameConfiguration source)
        {
            GameId = source.GameId;
            SettingsOverride = source.SettingsOverride == null
                ? null
                : new GameSettingsOverride(source.SettingsOverride);
        }

        /// <summary>The Playnite game ID this configuration belongs to.</summary>
        public Guid GameId { get; set; }

        /// <summary>Sparse per-game overrides of the global defaults, or null to inherit all of them.</summary>
        public GameSettingsOverride SettingsOverride { get; set; }
    }
}
