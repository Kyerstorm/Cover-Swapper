using System;

namespace PluginCoverShuffle.Infrastructure.Providers
{
    /// <summary>
    /// Reads whatever artwork Playnite already has for a game. Owned by
    /// Infrastructure (an outbound port) so <see cref="PlayniteMetadataCoverProvider"/>
    /// never depends on the Playnite Integration layer or Playnite.SDK
    /// directly; the Playnite-aware implementation is injected in from the
    /// composition root.
    /// </summary>
    public interface IPlayniteArtworkSource
    {
        /// <summary>Returns the game's known artwork paths, or null if the game cannot be found.</summary>
        PlayniteGameArtwork GetArtwork(Guid gameId);
    }

    /// <summary>
    /// Absolute, on-disk paths to a game's Playnite-known artwork. Any field
    /// is null when that artwork isn't set or couldn't be resolved to a real
    /// local file (e.g. a bare remote URL some library plugins store).
    /// </summary>
    public class PlayniteGameArtwork
    {
        public string CoverImagePath { get; set; }

        public string BackgroundImagePath { get; set; }

        public string IconPath { get; set; }
    }
}
