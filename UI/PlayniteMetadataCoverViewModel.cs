using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.UI
{
    /// <summary>
    /// Backs the "Add Cover -> Playnite Metadata" window: shows whatever
    /// cover/background/icon artwork Playnite already has for the game, so
    /// the user can pull it into the shuffle pool without any external
    /// provider or API key. Uses the provider-neutral <see cref="PlayniteMetadataArtworkItem"/>
    /// rather than the SteamGridDB-shaped <see cref="SteamGridDbResultItem"/>,
    /// so this view never has to reason about SteamGridDB concepts.
    /// </summary>
    public class PlayniteMetadataCoverViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverProvider _provider;
        private readonly CoverImportService _importService;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverShuffleLogger _logger;

        private static readonly string[] ViewStatePropertyNames =
        {
            nameof(ShowLoading), nameof(ShowEmpty), nameof(ShowError), nameof(ShowResults)
        };

        private static readonly string[] SlotPropertyNames =
        {
            nameof(RemainingSlots), nameof(AvailableToAddCount), nameof(IsAtCoverLimit),
            nameof(CanAddAll), nameof(AddAllButtonText), nameof(SlotsSummaryText), nameof(UsedCoversCount)
        };

        public string GameName { get; }

        public ObservableCollection<PlayniteMetadataArtworkItem> Results { get; } = new ObservableCollection<PlayniteMetadataArtworkItem>();

        private bool _isBusy;

        /// <summary>True while the initial artwork search is in flight.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetValue(ref _isBusy, value, new[] { nameof(IsBusy) }.Concat(ViewStatePropertyNames).Concat(SlotPropertyNames).ToArray());
        }

        private bool _hasLoaded;

        public bool HasLoaded
        {
            get => _hasLoaded;
            set => SetValue(ref _hasLoaded, value, new[] { nameof(HasLoaded) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private string _loadErrorMessage;

        /// <summary>Set when the provider search itself failed; distinct from a per-item add failure.</summary>
        public string LoadErrorMessage
        {
            get => _loadErrorMessage;
            set => SetValue(ref _loadErrorMessage, value, new[] { nameof(LoadErrorMessage) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        public bool ShowLoading => IsBusy && !HasLoaded;

        public bool ShowError => HasLoaded && !IsBusy && LoadErrorMessage != null;

        public bool ShowEmpty => HasLoaded && !IsBusy && LoadErrorMessage == null && Results.Count == 0;

        public bool ShowResults => HasLoaded && LoadErrorMessage == null && Results.Count > 0;

        public int UsedCoversCount => _repository.GetCovers(_gameId).Count;

        public int RemainingSlots => Math.Max(0, CoverLimitPolicy.MaxCoversPerGame - UsedCoversCount);

        public int AvailableToAddCount => Results.Count(r => r.IsAvailable && !r.AlreadyAdded);

        public bool IsAtCoverLimit => RemainingSlots <= 0;

        public bool CanAddAll => !IsBusy && AvailableToAddCount > 0 && RemainingSlots > 0;

        public string AddAllButtonText => AvailableToAddCount <= RemainingSlots ? "Add all" : "Add available artwork";

        /// <summary>
        /// Summary line shown above the "Add all" action. Distinguishes three
        /// distinct situations (plenty of slots / only some slots / none
        /// left) rather than a single generic count, matching the product
        /// examples for this window.
        /// </summary>
        public string SlotsSummaryText
        {
            get
            {
                if (IsAtCoverLimit)
                {
                    return "Cover limit reached";
                }

                if (AvailableToAddCount == 0)
                {
                    return null;
                }

                if (AvailableToAddCount > RemainingSlots)
                {
                    return RemainingSlots == 1
                        ? "Only 1 cover slot is available."
                        : $"Only {RemainingSlots} cover slots are available.";
                }

                var itemWord = AvailableToAddCount == 1 ? "artwork item" : "artwork items";
                return $"{AvailableToAddCount} {itemWord} available. You have {UsedCoversCount}/{CoverLimitPolicy.MaxCoversPerGame} covers.";
            }
        }

        public string CoverLimitMessage =>
            $"This game already has {CoverLimitPolicy.MaxCoversPerGame} covers. Remove a cover before adding another.";

        public PlayniteMetadataCoverViewModel(
            Guid gameId,
            string gameName,
            ICoverProvider provider,
            CoverImportService importService,
            ICoverShuffleRepository repository,
            ICoverShuffleLogger logger)
        {
            _gameId = gameId;
            GameName = string.IsNullOrWhiteSpace(gameName) ? "(game not found in Playnite)" : gameName;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            LoadErrorMessage = null;
            StatusMessage = null;
            Results.Clear();

            try
            {
                var searchResult = await _provider.SearchAsync(new CoverSearchRequest { GameId = _gameId }).ConfigureAwait(true);
                if (!searchResult.Success)
                {
                    LoadErrorMessage = searchResult.ErrorMessage;
                    return;
                }

                var existingSourceIds = new HashSet<string>(
                    _repository.GetCovers(_gameId)
                        .Where(c => c.Source == CoverSource.PlayniteMetadata && c.SourceId != null)
                        .Select(c => c.SourceId));

                foreach (var asset in searchResult.Assets)
                {
                    var alreadyAdded = existingSourceIds.Contains(asset.SourceId);
                    var isAvailable = !string.IsNullOrWhiteSpace(asset.FilePath) && File.Exists(asset.FilePath);
                    Results.Add(new PlayniteMetadataArtworkItem(asset, ParseArtworkType(asset.SourceId), alreadyAdded, isAvailable));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Loading Playnite metadata artwork failed unexpectedly.");
                LoadErrorMessage = "Something went wrong loading this game's artwork.";
            }
            finally
            {
                HasLoaded = true;
                IsBusy = false;
                RefreshCanAddState();
            }
        }

        public async Task AddAsync(PlayniteMetadataArtworkItem item)
        {
            if (item == null || item.AlreadyAdded || item.IsAdding || !item.IsAvailable || IsAtCoverLimit)
            {
                return;
            }

            item.ErrorMessage = null;
            item.IsAdding = true;
            try
            {
                var downloadResult = await _provider.DownloadAsync(item.Asset).ConfigureAwait(true);
                if (!downloadResult.Success)
                {
                    item.ErrorMessage = downloadResult.ErrorMessage;
                    return;
                }

                var importResult = _importService.Import(_gameId, new CoverAsset
                {
                    Source = item.Asset.Source,
                    SourceId = item.Asset.SourceId,
                    FilePath = downloadResult.LocalFilePath,
                    PreviewUrl = item.Asset.PreviewUrl,
                    FullImageUrl = item.Asset.FullImageUrl
                });

                if (!importResult.IsSuccess)
                {
                    item.ErrorMessage = importResult.Message;
                    return;
                }

                item.AlreadyAdded = true;
                StatusMessage = $"{item.DisplayName} added.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Adding Playnite metadata artwork failed unexpectedly.");
                item.ErrorMessage = "Something went wrong adding that artwork.";
            }
            finally
            {
                item.IsAdding = false;
                RefreshCanAddState();
            }
        }

        /// <summary>Re-attempts adding an item that previously failed, clearing its error state first.</summary>
        public Task RetryAsync(PlayniteMetadataArtworkItem item) => AddAsync(item);

        /// <summary>
        /// Adds every available, not-yet-added item up to however many cover
        /// slots remain. Stops early (rather than failing outright) once the
        /// limit is hit, since <see cref="CoverImportService"/> remains the
        /// single authority on the 10-cover cap - this just avoids calling it
        /// for artwork it would only reject anyway.
        /// </summary>
        public async Task AddAllAsync()
        {
            if (!CanAddAll)
            {
                return;
            }

            var candidates = Results.Where(r => r.IsAvailable && !r.AlreadyAdded && !r.IsAdding).ToList();
            var added = 0;
            var failed = 0;

            foreach (var item in candidates)
            {
                if (IsAtCoverLimit)
                {
                    break;
                }

                var wasAlreadyAdded = item.AlreadyAdded;
                await AddAsync(item).ConfigureAwait(true);

                if (!wasAlreadyAdded && item.AlreadyAdded)
                {
                    added++;
                }
                else if (item.HasError)
                {
                    failed++;
                }
            }

            StatusMessage = failed == 0
                ? $"Added {added} artwork item(s)."
                : $"Added {added} artwork item(s); {failed} failed.";
        }

        /// <summary>
        /// Recomputes every card's <see cref="PlayniteMetadataArtworkItem.CanAdd"/>
        /// against the current cover-limit state. Called after load and
        /// after every add, since adding a cover can itself push the game to
        /// the limit and must immediately disable the remaining cards.
        /// </summary>
        private void RefreshCanAddState()
        {
            var atLimit = IsAtCoverLimit;
            foreach (var item in Results)
            {
                item.CanAdd = item.IsAvailable && !item.AlreadyAdded && !item.IsAdding && !atLimit;
            }

            OnPropertyChanged(nameof(RemainingSlots));
            OnPropertyChanged(nameof(AvailableToAddCount));
            OnPropertyChanged(nameof(IsAtCoverLimit));
            OnPropertyChanged(nameof(CanAddAll));
            OnPropertyChanged(nameof(AddAllButtonText));
            OnPropertyChanged(nameof(SlotsSummaryText));
            OnPropertyChanged(nameof(UsedCoversCount));
        }

        private static PlayniteArtworkType ParseArtworkType(string sourceId)
        {
            switch (sourceId)
            {
                case "background":
                    return PlayniteArtworkType.Background;
                case "icon":
                    return PlayniteArtworkType.Icon;
                default:
                    return PlayniteArtworkType.Cover;
            }
        }
    }
}
