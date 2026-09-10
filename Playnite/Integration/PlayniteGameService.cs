using System;
using System.IO;
using Playnite.SDK;
using PluginCoverShuffle.Infrastructure.Providers;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Implements <see cref="IPlayniteGameService"/> and <see cref="IPlayniteArtworkSource"/>
    /// against the real Playnite API. This is the only class that should
    /// read or write <c>Game.CoverImage</c>/<c>BackgroundImage</c>/<c>Icon</c>
    /// directly.
    /// </summary>
    public class PlayniteGameService : IPlayniteGameService, IPlayniteArtworkSource
    {
        private readonly IPlayniteAPI _api;

        public PlayniteGameService(IPlayniteAPI api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public string GetCoverReference(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            return game?.CoverImage;
        }

        public void SetCoverReference(Guid gameId, string coverReference)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null)
            {
                return;
            }

            game.CoverImage = coverReference;
            _api.Database.Games.Update(game);
        }

        public string GetGameName(Guid gameId)
        {
            return _api.Database.Games.Get(gameId)?.Name;
        }

        public bool IsGameInstalled(Guid gameId)
        {
            return _api.Database.Games.Get(gameId)?.IsInstalled ?? false;
        }

        public PlayniteGameArtwork GetArtwork(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null)
            {
                return null;
            }

            return new PlayniteGameArtwork
            {
                CoverImagePath = ResolveToLocalFile(game.CoverImage),
                BackgroundImagePath = ResolveToLocalFile(game.BackgroundImage),
                IconPath = ResolveToLocalFile(game.Icon)
            };
        }

        /// <summary>
        /// Resolves a Playnite image reference (relative to its library
        /// files, or already absolute) to a real file on disk. Returns null
        /// for anything that isn't a set, resolvable local file — e.g. a
        /// bare remote URL some library plugins store instead of a path,
        /// which this phase does not fetch.
        /// </summary>
        private string ResolveToLocalFile(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            try
            {
                var fullPath = _api.Database.GetFullFilePath(reference);
                return !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath) ? fullPath : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
