using System.Collections.Generic;
using PluginCoverShuffle.Domain.Providers;

namespace PluginCoverShuffle.UI
{
    /// <summary>One search result shown in the SteamGridDB grid, with mutable "already added" UI state.</summary>
    public class SteamGridDbResultItem : ObservableObject
    {
        public CoverAsset Asset { get; }

        public string PreviewUrl => Asset.PreviewUrl;

        private bool _alreadyAdded;

        public bool AlreadyAdded
        {
            get => _alreadyAdded;
            set => SetValue(ref _alreadyAdded, value);
        }

        public SteamGridDbResultItem(CoverAsset asset, bool alreadyAdded)
        {
            Asset = asset;
            AlreadyAdded = alreadyAdded;
        }

        /// <summary>Screen-reader text for this card, since the image itself carries no alt text.</summary>
        public string AccessibleDescription =>
            AlreadyAdded ? "SteamGridDB cover, already added to this game" : "SteamGridDB cover";
    }
}
