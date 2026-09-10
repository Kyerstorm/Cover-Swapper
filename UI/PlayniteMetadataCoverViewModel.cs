using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// provider or API key. Reuses <see cref="SteamGridDbResultItem"/> since
    /// its shape (asset + already-added flag) isn't SteamGridDB-specific.
    /// </summary>
    public class PlayniteMetadataCoverViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverProvider _provider;
        private readonly CoverImportService _importService;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverShuffleLogger _logger;

        public ObservableCollection<SteamGridDbResultItem> Results { get; } = new ObservableCollection<SteamGridDbResultItem>();

        private string _statusMessage;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set => SetValue(ref _isBusy, value);
        }

        public PlayniteMetadataCoverViewModel(
            Guid gameId,
            ICoverProvider provider,
            CoverImportService importService,
            ICoverShuffleRepository repository,
            ICoverShuffleLogger logger)
        {
            _gameId = gameId;
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
            StatusMessage = "Loading...";
            Results.Clear();

            try
            {
                var searchResult = await _provider.SearchAsync(new CoverSearchRequest { GameId = _gameId }).ConfigureAwait(true);
                if (!searchResult.Success)
                {
                    StatusMessage = searchResult.ErrorMessage;
                    return;
                }

                var existingSourceIds = new HashSet<string>(
                    _repository.GetCovers(_gameId)
                        .Where(c => c.Source == CoverSource.PlayniteMetadata && c.SourceId != null)
                        .Select(c => c.SourceId));

                foreach (var asset in searchResult.Assets)
                {
                    Results.Add(new SteamGridDbResultItem(asset, existingSourceIds.Contains(asset.SourceId)));
                }

                StatusMessage = Results.Count == 0 ? "No artwork available." : $"{Results.Count} artwork item(s) found.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Loading Playnite metadata artwork failed unexpectedly.");
                StatusMessage = "Something went wrong loading this game's artwork.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task AddAsync(SteamGridDbResultItem item)
        {
            if (item == null || item.AlreadyAdded || IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "Adding...";
            try
            {
                var downloadResult = await _provider.DownloadAsync(item.Asset).ConfigureAwait(true);
                if (!downloadResult.Success)
                {
                    StatusMessage = downloadResult.ErrorMessage;
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
                    StatusMessage = importResult.Message;
                    return;
                }

                item.AlreadyAdded = true;
                StatusMessage = "Cover added.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Adding Playnite metadata artwork failed unexpectedly.");
                StatusMessage = "Something went wrong adding that cover.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
