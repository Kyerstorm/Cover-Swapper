using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.Infrastructure.Providers
{
    /// <summary>
    /// Exposes artwork Playnite already has for a game (cover, background,
    /// icon) as normalized <see cref="CoverAsset"/> candidates. Reads
    /// directly from <see cref="IPlayniteArtworkSource"/> on every search
    /// rather than keeping its own copy, so this never duplicates or goes
    /// stale relative to Playnite's own metadata.
    /// </summary>
    public class PlayniteMetadataCoverProvider : ICoverProvider
    {
        private readonly IPlayniteArtworkSource _artworkSource;

        public CoverSource Source => CoverSource.PlayniteMetadata;

        public PlayniteMetadataCoverProvider(IPlayniteArtworkSource artworkSource)
        {
            _artworkSource = artworkSource ?? throw new ArgumentNullException(nameof(artworkSource));
        }

        public Task<CoverSearchResult> SearchAsync(CoverSearchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var artwork = _artworkSource.GetArtwork(request.GameId);
            if (artwork == null)
            {
                return Task.FromResult(CoverSearchResult.Failed("Could not read this game's Playnite metadata."));
            }

            var assets = new List<CoverAsset>();
            AddIfPresent(assets, artwork.CoverImagePath, "cover");
            AddIfPresent(assets, artwork.BackgroundImagePath, "background");
            AddIfPresent(assets, artwork.IconPath, "icon");

            if (assets.Count == 0)
            {
                return Task.FromResult(CoverSearchResult.Failed("This game has no artwork in Playnite yet."));
            }

            return Task.FromResult(CoverSearchResult.Succeeded(assets.ToArray()));
        }

        public Task<CoverDownloadResult> DownloadAsync(CoverAsset asset)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.Source != CoverSource.PlayniteMetadata || string.IsNullOrWhiteSpace(asset.FilePath) || !File.Exists(asset.FilePath))
            {
                return Task.FromResult(CoverDownloadResult.Failed("This artwork is no longer available."));
            }

            // The file already lives on disk under Playnite's own storage; no
            // copy happens here. CoverImportService performs the actual copy
            // into plugin-owned storage, so Playnite's file is never touched.
            return Task.FromResult(CoverDownloadResult.Succeeded(asset.FilePath));
        }

        private static void AddIfPresent(List<CoverAsset> assets, string filePath, string sourceId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            assets.Add(new CoverAsset
            {
                Source = CoverSource.PlayniteMetadata,
                SourceId = sourceId,
                FilePath = filePath,
                PreviewUrl = filePath
            });
        }
    }
}
