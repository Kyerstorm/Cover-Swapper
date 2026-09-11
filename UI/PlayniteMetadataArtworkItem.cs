using System.Collections.Generic;
using Playnite.SDK;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// The kind of Playnite-known artwork a <see cref="PlayniteMetadataArtworkItem"/>
    /// wraps. Distinct from other providers' concepts (e.g. SteamGridDB grid
    /// types) - Playnite only ever exposes these three slots per game.
    /// </summary>
    public enum PlayniteArtworkType
    {
        Cover,
        Background,
        Icon
    }

    /// <summary>
    /// One artwork card shown in the "Add Cover -> Playnite Metadata" window.
    /// Provider-neutral: carries only what that window needs to render and
    /// act on a candidate, so this view never has to reason about
    /// SteamGridDB-specific concepts (see <see cref="SteamGridDbResultItem"/>,
    /// which this type replaces for the Playnite Metadata source).
    /// </summary>
    public class PlayniteMetadataArtworkItem : ObservableObject
    {
        public CoverAsset Asset { get; }

        public PlayniteArtworkType ArtworkType { get; }

        public string DisplayName { get; }

        public string PreviewPath => Asset.PreviewUrl;

        public string SourceId => Asset.SourceId;

        /// <summary>
        /// Suggested preview image width/height for this artwork's natural
        /// aspect ratio (portrait cover, wide background, square icon) -
        /// computed once here rather than forcing every card into identical
        /// dimensions or pushing per-type layout logic into XAML.
        /// </summary>
        public double ImageWidth { get; }

        public double ImageHeight { get; }

        private bool _alreadyAdded;

        public bool AlreadyAdded
        {
            get => _alreadyAdded;
            set => SetValue(ref _alreadyAdded, value, nameof(AlreadyAdded), nameof(StatusText));
        }

        /// <summary>
        /// Whether the underlying file Playnite recorded for this artwork
        /// still exists on disk. Checked once at load time against real
        /// filesystem state - never assumed just because Playnite has a path
        /// on record for it.
        /// </summary>
        private bool _isAvailable = true;

        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetValue(ref _isAvailable, value, nameof(IsAvailable), nameof(StatusText));
        }

        private bool _isAdding;

        public bool IsAdding
        {
            get => _isAdding;
            set => SetValue(ref _isAdding, value, nameof(IsAdding), nameof(StatusText));
        }

        private string _errorMessage;

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetValue(ref _errorMessage, value, nameof(ErrorMessage), nameof(HasError), nameof(StatusText));
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Whether the Add action is currently allowed for this card. Owned
        /// and recomputed by <see cref="PlayniteMetadataCoverViewModel"/>
        /// (the only place that knows the game's remaining cover-slot count),
        /// rather than each card guessing at global state on its own.
        /// </summary>
        private bool _canAdd;

        public bool CanAdd
        {
            get => _canAdd;
            set => SetValue(ref _canAdd, value);
        }

        /// <summary>Short status label shown under the artwork type name (e.g. "Added", "Unavailable").</summary>
        public string StatusText
        {
            get
            {
                if (AlreadyAdded)
                {
                    return "✓ Added";
                }

                if (IsAdding)
                {
                    return "Adding...";
                }

                if (!IsAvailable)
                {
                    return "Unavailable";
                }

                if (HasError)
                {
                    return "Couldn't add this artwork.";
                }

                return null;
            }
        }

        public string AccessibleDescription =>
            AlreadyAdded ? $"{DisplayName}, already added to this game" : $"{DisplayName} from Playnite metadata";

        public PlayniteMetadataArtworkItem(CoverAsset asset, PlayniteArtworkType artworkType, bool alreadyAdded, bool isAvailable)
        {
            Asset = asset;
            ArtworkType = artworkType;
            DisplayName = artworkType.ToString();
            AlreadyAdded = alreadyAdded;
            IsAvailable = isAvailable;

            switch (artworkType)
            {
                case PlayniteArtworkType.Background:
                    ImageWidth = 220;
                    ImageHeight = 124;
                    break;
                case PlayniteArtworkType.Icon:
                    ImageWidth = 96;
                    ImageHeight = 96;
                    break;
                default:
                    ImageWidth = 130;
                    ImageHeight = 180;
                    break;
            }
        }
    }
}
