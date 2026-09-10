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
    /// Backs the "Add Cover -> SteamGridDB" window: search, a selectable
    /// preview grid, and add-selected. Contains no WPF dependency beyond
    /// <see cref="ObservableObject"/>, so search/selection/add orchestration
    /// is testable without a real window.
    /// </summary>
    public class SteamGridDbSearchViewModel : ObservableObject
    {
        private readonly Guid _gameId;
        private readonly ICoverProvider _provider;
        private readonly CoverImportService _importService;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverShuffleLogger _logger;

        private static readonly string[] ViewStatePropertyNames =
        {
            nameof(ShowLoading), nameof(ShowError), nameof(ShowEmpty), nameof(ShowResults), nameof(ShowGameMatches)
        };

        public ObservableCollection<SteamGridDbResultItem> Results { get; } = new ObservableCollection<SteamGridDbResultItem>();

        /// <summary>
        /// Candidate games returned when a search query matched more than
        /// one game on the provider (e.g. "Fallout"). Populated instead of
        /// <see cref="Results"/> until the user picks one via
        /// <see cref="SelectGameMatchAsync"/>.
        /// </summary>
        public ObservableCollection<CoverGameMatch> GameMatches { get; } = new ObservableCollection<CoverGameMatch>();

        /// <summary>The Playnite game this dialog was opened for; used only to prefill the search box.</summary>
        public string GameName { get; }

        private string _searchQuery;

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetValue(ref _searchQuery, value);
        }

        private bool _isSearching;

        /// <summary>True only while a search is in flight; drives the full-grid loading state.</summary>
        public bool IsSearching
        {
            get => _isSearching;
            set => SetValue(ref _isSearching, value,
                new[] { nameof(IsSearching), nameof(IsBusy), nameof(CanAddSelected), nameof(CanSelectGameMatch) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private bool _isAdding;

        /// <summary>True only while the selected cover is downloading/importing.</summary>
        public bool IsAdding
        {
            get => _isAdding;
            set => SetValue(ref _isAdding, value, nameof(IsAdding), nameof(IsBusy), nameof(CanAddSelected), nameof(CanSelectGameMatch));
        }

        /// <summary>True while any network/import operation is in flight.</summary>
        public bool IsBusy => IsSearching || IsAdding;

        private bool _hasSearched;

        /// <summary>True once a search has completed at least once; gates the empty-results state.</summary>
        public bool HasSearched
        {
            get => _hasSearched;
            set => SetValue(ref _hasSearched, value, new[] { nameof(HasSearched) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private bool _hasResults;

        public bool HasResults
        {
            get => _hasResults;
            set => SetValue(ref _hasResults, value, new[] { nameof(HasResults) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private bool _hasGameMatches;

        /// <summary>True when the last search returned multiple candidate games awaiting disambiguation.</summary>
        public bool HasGameMatches
        {
            get => _hasGameMatches;
            set => SetValue(ref _hasGameMatches, value, new[] { nameof(HasGameMatches) }.Concat(ViewStatePropertyNames).ToArray());
        }

        private string _errorMessage;

        /// <summary>Set when a search or add operation fails; null when there is no error to show.</summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetValue(ref _errorMessage, value, new[] { nameof(ErrorMessage) }.Concat(ViewStatePropertyNames).ToArray());
        }

        /// <summary>Full-grid loading indicator, shown only while searching.</summary>
        public bool ShowLoading => IsSearching;

        /// <summary>Shown when the last search or add failed with a real error.</summary>
        public bool ShowError => !IsSearching && ErrorMessage != null;

        /// <summary>Shown after a search that returned zero covers.</summary>
        public bool ShowEmpty => !IsSearching && ErrorMessage == null && HasSearched && !HasResults && !HasGameMatches;

        /// <summary>Shown when there are covers to display.</summary>
        public bool ShowResults => !IsSearching && ErrorMessage == null && HasResults && !HasGameMatches;

        /// <summary>Shown when the search matched multiple games and the user must pick one before covers are shown.</summary>
        public bool ShowGameMatches => !IsSearching && ErrorMessage == null && HasGameMatches;

        private string _statusMessage;

        /// <summary>Short transient status text (e.g. "Cover added."), shown in the bottom bar.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetValue(ref _statusMessage, value);
        }

        private string _resultCountText;

        public string ResultCountText
        {
            get => _resultCountText;
            set => SetValue(ref _resultCountText, value);
        }

        private string _gameMatchCountText;

        public string GameMatchCountText
        {
            get => _gameMatchCountText;
            set => SetValue(ref _gameMatchCountText, value);
        }

        private bool _isAtCoverLimit;

        public bool IsAtCoverLimit
        {
            get => _isAtCoverLimit;
            set => SetValue(ref _isAtCoverLimit, value, nameof(IsAtCoverLimit), nameof(CanAddSelected));
        }

        public string CoverLimitMessage => $"This game already has the maximum of {CoverLimitPolicy.MaxCoversPerGame} covers.";

        private SteamGridDbResultItem _selectedItem;

        public SteamGridDbResultItem SelectedItem
        {
            get => _selectedItem;
            set => SetValue(ref _selectedItem, value, nameof(SelectedItem), nameof(CanAddSelected), nameof(SelectedCountText));
        }

        /// <summary>Whether "Add Selected Cover" should be enabled.</summary>
        public bool CanAddSelected => SelectedItem != null && !SelectedItem.AlreadyAdded && !IsBusy && !IsAtCoverLimit;

        public string SelectedCountText => $"Selected: {(SelectedItem == null ? 0 : 1)}";

        private CoverGameMatch _selectedGameMatch;

        public CoverGameMatch SelectedGameMatch
        {
            get => _selectedGameMatch;
            set => SetValue(ref _selectedGameMatch, value, nameof(SelectedGameMatch), nameof(CanSelectGameMatch));
        }

        /// <summary>Whether "Select This Game" should be enabled.</summary>
        public bool CanSelectGameMatch => SelectedGameMatch != null && !IsBusy;

        public SteamGridDbSearchViewModel(
            Guid gameId,
            string gameName,
            ICoverProvider provider,
            CoverImportService importService,
            ICoverShuffleRepository repository,
            ICoverShuffleLogger logger)
        {
            _gameId = gameId;
            GameName = gameName;
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!string.IsNullOrWhiteSpace(gameName))
            {
                SearchQuery = gameName;
            }

            RefreshCoverLimitState();
        }

        public async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ErrorMessage = null;
                StatusMessage = "Enter a game name to search.";
                return;
            }

            if (IsBusy)
            {
                return;
            }

            IsSearching = true;
            ErrorMessage = null;
            StatusMessage = null;
            HasResults = false;
            HasGameMatches = false;
            SelectedItem = null;
            SelectedGameMatch = null;
            Results.Clear();
            GameMatches.Clear();

            try
            {
                var searchResult = await _provider.SearchAsync(new CoverSearchRequest { GameId = _gameId, Query = SearchQuery }).ConfigureAwait(true);
                if (!searchResult.Success)
                {
                    ErrorMessage = searchResult.ErrorMessage;
                    return;
                }

                if (searchResult.RequiresGameSelection)
                {
                    foreach (var match in searchResult.GameMatches)
                    {
                        GameMatches.Add(match);
                    }

                    HasGameMatches = GameMatches.Count > 0;
                    GameMatchCountText = GameMatches.Count == 1 ? "1 matching game" : $"{GameMatches.Count} matching games";
                    return;
                }

                PopulateResults(searchResult);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SteamGridDB search failed unexpectedly.");
                ErrorMessage = "Something went wrong searching SteamGridDB.";
            }
            finally
            {
                HasSearched = true;
                IsSearching = false;
                RefreshCoverLimitState();
            }
        }

        /// <summary>
        /// Fetches covers for one of the candidate games returned by a
        /// prior ambiguous search, replacing the game-match list with the
        /// resulting cover grid.
        /// </summary>
        public async Task SelectGameMatchAsync(CoverGameMatch match)
        {
            if (match == null || IsBusy)
            {
                return;
            }

            IsSearching = true;
            ErrorMessage = null;
            StatusMessage = null;
            HasResults = false;
            SelectedItem = null;
            Results.Clear();

            try
            {
                var searchResult = await _provider.SearchAsync(new CoverSearchRequest
                {
                    GameId = _gameId,
                    Query = SearchQuery,
                    SelectedProviderGameId = match.ProviderGameId,
                    SelectedProviderGameName = match.Name
                }).ConfigureAwait(true);

                if (!searchResult.Success)
                {
                    ErrorMessage = searchResult.ErrorMessage;
                    return;
                }

                HasGameMatches = false;
                GameMatches.Clear();
                PopulateResults(searchResult);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SteamGridDB search failed unexpectedly.");
                ErrorMessage = "Something went wrong searching SteamGridDB.";
            }
            finally
            {
                IsSearching = false;
                RefreshCoverLimitState();
            }
        }

        private void PopulateResults(CoverSearchResult searchResult)
        {
            var existingSourceIds = new HashSet<string>(
                _repository.GetCovers(_gameId)
                    .Where(c => c.Source == CoverSource.SteamGridDb && c.SourceId != null)
                    .Select(c => c.SourceId));

            foreach (var asset in searchResult.Assets)
            {
                Results.Add(new SteamGridDbResultItem(asset, existingSourceIds.Contains(asset.SourceId)));
            }

            HasResults = Results.Count > 0;
            ResultCountText = Results.Count == 1 ? "1 cover found" : $"{Results.Count} covers found";
        }

        /// <summary>Downloads and imports whichever cover is currently selected.</summary>
        public Task AddSelectedAsync() => AddAsync(SelectedItem);

        public async Task AddAsync(SteamGridDbResultItem item)
        {
            if (item == null || item.AlreadyAdded || IsBusy || IsAtCoverLimit)
            {
                return;
            }

            IsAdding = true;
            ErrorMessage = null;
            StatusMessage = "Downloading...";
            try
            {
                var downloadResult = await _provider.DownloadAsync(item.Asset).ConfigureAwait(true);
                if (!downloadResult.Success)
                {
                    StatusMessage = null;
                    ErrorMessage = downloadResult.ErrorMessage;
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
                    StatusMessage = null;
                    ErrorMessage = importResult.Message;
                    return;
                }

                item.AlreadyAdded = true;
                StatusMessage = "Cover added.";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Adding a SteamGridDB cover failed unexpectedly.");
                StatusMessage = null;
                ErrorMessage = "Something went wrong adding that cover.";
            }
            finally
            {
                IsAdding = false;
                RefreshCoverLimitState();
                OnPropertyChanged(nameof(CanAddSelected));
            }
        }

        private void RefreshCoverLimitState()
        {
            IsAtCoverLimit = _repository.GetCovers(_gameId).Count >= CoverLimitPolicy.MaxCoversPerGame;
        }
    }
}
