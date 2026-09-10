using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Infrastructure.Persistence;
using PluginCoverShuffle.Infrastructure.Providers;
using PluginCoverShuffle.Infrastructure.Storage;
using PluginCoverShuffle.Services;
using PluginCoverShuffle.UI;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Builds the "Plugin Cover Shuffle" right-click game menu. Playnite calls
    /// <see cref="BuildMenuItems"/> fresh on every menu open, so no state is
    /// cached here and no duplicate entries can accumulate.
    /// </summary>
    public class CoverShuffleGameMenuFactory
    {
        private const string MenuSectionName = "Cover Shuffle";

        private readonly PlayniteCoverService _coverService;
        private readonly CoverImportService _importService;
        private readonly ICoverProvider _steamGridDbProvider;
        private readonly ICoverProvider _playniteMetadataProvider;
        private readonly ICoverShuffleRepository _repository;
        private readonly ICoverStorage _storage;
        private readonly IDialogsFactory _dialogs;
        private readonly ICoverShuffleLogger _logger;
        private readonly IPlayniteGameService _gameService;
        private readonly LocalFileCoverProvider _localFileCoverProvider = new LocalFileCoverProvider();

        public CoverShuffleGameMenuFactory(
            PlayniteCoverService coverService,
            CoverImportService importService,
            ICoverProvider steamGridDbProvider,
            ICoverProvider playniteMetadataProvider,
            ICoverShuffleRepository repository,
            ICoverStorage storage,
            IDialogsFactory dialogs,
            ICoverShuffleLogger logger,
            IPlayniteGameService gameService = null)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // These may all be null together when plugin storage failed to
            // initialize; menu falls back to a single informational item.
            _coverService = coverService;
            _importService = importService;
            _steamGridDbProvider = steamGridDbProvider;
            _playniteMetadataProvider = playniteMetadataProvider;
            _repository = repository;
            _storage = storage;
            _gameService = gameService;
        }

        public IEnumerable<GameMenuItem> BuildMenuItems(List<Game> games)
        {
            if (_coverService == null)
            {
                return new[]
                {
                    new GameMenuItem
                    {
                        Description = "Cover Shuffle is unavailable (storage failed to initialize)",
                        MenuSection = MenuSectionName,
                        Action = _ => { }
                    }
                };
            }

            // Visibility reflects state only for a single selected game; a
            // multi-selection shows every action and applies it per game.
            var singleGameId = games?.Count == 1 ? games[0].Id : (Guid?)null;
            var isEnabled = singleGameId.HasValue && _coverService.IsEnabled(singleGameId.Value);
            var hasSavedOriginal = singleGameId.HasValue && _coverService.HasSavedOriginalCover(singleGameId.Value);

            var items = new List<GameMenuItem>();

            if (!singleGameId.HasValue || !isEnabled)
            {
                items.Add(new GameMenuItem
                {
                    Description = "Enable Cover Shuffle",
                    MenuSection = MenuSectionName,
                    Action = args => ForEachGame(args, id => _coverService.EnableCoverShuffle(id))
                });
            }

            if (!singleGameId.HasValue || isEnabled)
            {
                items.Add(new GameMenuItem
                {
                    Description = "Disable Cover Shuffle",
                    MenuSection = MenuSectionName,
                    Action = args => ForEachGame(args, id => _coverService.DisableCoverShuffle(id))
                });
            }

            items.Add(new GameMenuItem
            {
                Description = "Local File",
                MenuSection = MenuSectionName + "|Add Cover",
                Action = args => ForEachGame(args, AddLocalFileCover)
            });

            if (_steamGridDbProvider != null)
            {
                items.Add(new GameMenuItem
                {
                    Description = "SteamGridDB",
                    MenuSection = MenuSectionName + "|Add Cover",
                    Action = args => ForEachGame(args, id =>
                    {
                        var gameName = _gameService?.GetGameName(id);
                        var viewModel = new SteamGridDbSearchViewModel(id, gameName, _steamGridDbProvider, _importService, _repository, _logger);
                        SteamGridDbSearchView.ShowDialog(_dialogs, viewModel);
                    })
                });
            }

            if (_playniteMetadataProvider != null)
            {
                items.Add(new GameMenuItem
                {
                    Description = "Playnite Metadata",
                    MenuSection = MenuSectionName + "|Add Cover",
                    Action = args => ForEachGame(args, id =>
                    {
                        var viewModel = new PlayniteMetadataCoverViewModel(id, _playniteMetadataProvider, _importService, _repository, _logger);
                        new PlayniteMetadataCoverWindow(viewModel).ShowDialog();
                    })
                });
            }

            items.Add(new GameMenuItem
            {
                Description = "Shuffle Now",
                MenuSection = MenuSectionName,
                Action = args =>
                {
                    var failures = new List<string>();
                    ForEachGame(args, id =>
                    {
                        var result = _coverService.ShuffleToNextCover(id);
                        if (!result.Success)
                        {
                            failures.Add(result.Message);
                        }
                    });

                    if (failures.Count > 0)
                    {
                        _dialogs.ShowMessage(string.Join(Environment.NewLine, failures.Distinct()));
                    }
                }
            });

            items.Add(new GameMenuItem
            {
                Description = "Manage Covers",
                MenuSection = MenuSectionName,
                Action = args => ForEachGame(args, id =>
                {
                    var viewModel = new CoverManagementViewModel(id, _repository, _storage);
                    new ManageCoversWindow(viewModel).ShowDialog();
                })
            });

            if (!singleGameId.HasValue || hasSavedOriginal)
            {
                items.Add(new GameMenuItem
                {
                    Description = "Restore Original Cover",
                    MenuSection = MenuSectionName,
                    Action = args => ForEachGame(args, id => _coverService.RestoreOriginalCover(id))
                });
            }

            return items;
        }

        private void AddLocalFileCover(Guid gameId)
        {
            var filePath = _dialogs.SelectImagefile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            // Local files need no async network wait, so it's safe to block
            // synchronously here rather than round-trip through a window.
            var searchResult = _localFileCoverProvider.SearchAsync(new CoverSearchRequest
            {
                GameId = gameId,
                LocalFilePath = filePath
            }).GetAwaiter().GetResult();

            if (!searchResult.Success || searchResult.Assets.Count == 0)
            {
                _dialogs.ShowMessage(searchResult.ErrorMessage ?? "Could not read the selected file.");
                return;
            }

            var downloadResult = _localFileCoverProvider.DownloadAsync(searchResult.Assets[0]).GetAwaiter().GetResult();
            if (!downloadResult.Success)
            {
                _dialogs.ShowMessage(downloadResult.ErrorMessage);
                return;
            }

            var importResult = _importService.Import(gameId, new CoverAsset
            {
                Source = searchResult.Assets[0].Source,
                SourceId = searchResult.Assets[0].SourceId,
                FilePath = downloadResult.LocalFilePath
            });

            if (!importResult.IsSuccess)
            {
                _dialogs.ShowMessage(importResult.Message);
            }
        }

        private void ForEachGame(GameMenuItemActionArgs args, Action<Guid> action)
        {
            foreach (var game in args.Games ?? Enumerable.Empty<Game>().ToList())
            {
                try
                {
                    action(game.Id);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Cover Shuffle menu action failed for game '{game.Id}'.");
                }
            }
        }
    }
}
