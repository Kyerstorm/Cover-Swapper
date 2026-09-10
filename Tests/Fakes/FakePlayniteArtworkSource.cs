using System;
using System.Collections.Generic;
using PluginCoverShuffle.Infrastructure.Providers;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>In-memory fake of <see cref="IPlayniteArtworkSource"/> for testing <see cref="PlayniteMetadataCoverProvider"/> without a live Playnite installation.</summary>
    public class FakePlayniteArtworkSource : IPlayniteArtworkSource
    {
        private readonly Dictionary<Guid, PlayniteGameArtwork> _artworkByGame = new Dictionary<Guid, PlayniteGameArtwork>();

        public void Seed(Guid gameId, PlayniteGameArtwork artwork)
        {
            _artworkByGame[gameId] = artwork;
        }

        public PlayniteGameArtwork GetArtwork(Guid gameId)
        {
            return _artworkByGame.TryGetValue(gameId, out var artwork) ? artwork : null;
        }
    }
}
