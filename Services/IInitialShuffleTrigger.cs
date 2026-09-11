using System;

namespace PluginCoverShuffle.Services
{
    /// <summary>
    /// Narrow port <see cref="CoverImportService"/> uses to let a game's very
    /// first usable cover automatically become its active Playnite cover,
    /// without coupling the provider-independent import pipeline to the full
    /// Playnite integration layer (<c>PlayniteCoverService</c>, which
    /// implements this). Mirrors <see cref="Infrastructure.Providers.IPlayniteArtworkSource"/>'s
    /// role: the port is defined where it is consumed, and the
    /// Playnite-aware implementation is injected in from the composition root.
    /// </summary>
    public interface IInitialShuffleTrigger
    {
        /// <summary>
        /// Applies the game's first usable cover automatically if - and only
        /// if - it genuinely has none applied yet (no persisted shuffle state
        /// at all). Safe to call after every import regardless of source;
        /// a no-op once the game has shuffled even once before.
        /// </summary>
        void TriggerIfNeeded(Guid gameId);
    }
}
