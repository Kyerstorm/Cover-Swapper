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
    /// Backs the "Add Cover -> SteamGridDB" window: search, preview grid,
    /// and add. Contains no WPF dependency beyond <see cref="ObservableObject"/>,
    /// so search/add orchestration is testable without a real window.
    /// </summary>
    public class SteamGridDbSearchViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverProvider _provider;
        private readonly CoverImportService _importService;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverShuffleLogger _logger;

        public ObservableCollection<SteamGridDbResultItem> Results { get; } = new ObservableCollection<SteamGridDbResultItem>();

        private string _searchQuery;

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetValue(ref _searchQuery, value);
        }

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

        public SteamGridDbSearchViewModel(
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

        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                StatusMessage = "Enter a game name to search.";
                return;
            }

            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "Searching...";
            Results.Clear();

            try
            {
                var searchResult = await _provider.SearchAsync(new CoverSearchRequest { GameId = _gameId, Query = SearchQuery }).ConfigureAwait(true);
                if (!searchResult.Success)
                {
                    StatusMessage = searchResult.ErrorMessage;
                    return;
                }

                var existingSourceIds = new HashSet<string>(
                    _repository.GetCovers(_gameId)
                        .Where(c => c.Source == CoverSource.SteamGridDb && c.SourceId != null)
                        .Select(c => c.SourceId));

                foreach (var asset in searchResult.Assets)
                {
                    Results.Add(new SteamGridDbResultItem(asset, existingSourceIds.Contains(asset.SourceId)));
                }

                StatusMessage = Results.Count == 0 ? "No covers found." : $"{Results.Count} cover(s) found.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SteamGridDB search failed unexpectedly.");
                StatusMessage = "Something went wrong searching SteamGridDB.";
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
            StatusMessage = "Downloading...";
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
                _logger.Error(ex, "Adding a SteamGridDB cover failed unexpectedly.");
                StatusMessage = "Something went wrong adding that cover.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
