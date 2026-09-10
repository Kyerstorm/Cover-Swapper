using System;
using System.Linq;
using System.Windows;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Domain.Providers;
using PluginCoverShuffle.Infrastructure.Logging;
using PluginCoverShuffle.Services;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Applies the user's configured <see cref="NewGameBehavior"/> to a game
    /// Cover Shuffle has determined is new to it. Never contacts a network
    /// provider itself, regardless of behaviour — "Automatic" enables
    /// shuffling, captures the original cover so it can be restored later,
    /// and pulls in whatever cover artwork Playnite already has locally so
    /// the game has something to shuffle immediately. SteamGridDB searching
    /// always remains a separate, explicit user action.
    /// </summary>
    public class NewGameConfigurationService
    {
        private readonly PlayniteCoverService _coverService;
        private readonly ICoverProvider _playniteMetadataProvider;
        private readonly CoverImportService _importService;
        private readonly Func<CoverShuffleSettings> _globalSettingsProvider;
        private readonly IDialogsFactory _dialogs;
        private readonly ICoverShuffleLogger _logger;

        public NewGameConfigurationService(
            PlayniteCoverService coverService,
            ICoverProvider playniteMetadataProvider,
            CoverImportService importService,
            Func<CoverShuffleSettings> globalSettingsProvider,
            IDialogsFactory dialogs,
            ICoverShuffleLogger logger)
        {
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
            _playniteMetadataProvider = playniteMetadataProvider ?? throw new ArgumentNullException(nameof(playniteMetadataProvider));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _globalSettingsProvider = globalSettingsProvider ?? throw new ArgumentNullException(nameof(globalSettingsProvider));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void HandleNewGame(Guid gameId, string gameName)
        {
            var behavior = _globalSettingsProvider()?.NewGameBehavior ?? NewGameBehavior.DoNothing;

            switch (behavior)
            {
                case NewGameBehavior.DoNothing:
                    return;

                case NewGameBehavior.Ask:
                    AskAndMaybeEnable(gameId, gameName);
                    return;

                case NewGameBehavior.Automatic:
                    PrepareAutomatically(gameId);
                    return;
            }
        }

        /// <summary>
        /// Game installed -&gt; create Cover Shuffle configuration -&gt; pull in
        /// whatever local Playnite artwork exists for it. Deliberately stops
        /// there: no provider is queried and nothing waits on the network, so
        /// this always succeeds offline. Failure to find/import local
        /// artwork is not an error — the game is still enabled and ready for
        /// covers to be added later via the configured provider.
        /// </summary>
        private void PrepareAutomatically(Guid gameId)
        {
            _coverService.EnableCoverShuffle(gameId);

            try
            {
                ImportExistingPlayniteCover(gameId);
            }
            catch (Exception ex)
            {
                _logger.Warning($"Could not import existing Playnite artwork for new game '{gameId}': {ex.Message}");
            }

            _logger.Info($"Automatically prepared new game '{gameId}' for Cover Shuffle.");
        }

        private void ImportExistingPlayniteCover(Guid gameId)
        {
            var searchResult = _playniteMetadataProvider.SearchAsync(new CoverSearchRequest { GameId = gameId })
                .GetAwaiter().GetResult();
            if (!searchResult.Success)
            {
                return;
            }

            var coverAsset = searchResult.Assets.FirstOrDefault(a => a.SourceId == "cover");
            if (coverAsset == null)
            {
                return;
            }

            var downloadResult = _playniteMetadataProvider.DownloadAsync(coverAsset).GetAwaiter().GetResult();
            if (!downloadResult.Success)
            {
                return;
            }

            _importService.Import(gameId, new CoverAsset
            {
                Source = coverAsset.Source,
                SourceId = coverAsset.SourceId,
                FilePath = downloadResult.LocalFilePath,
                PreviewUrl = coverAsset.PreviewUrl,
                FullImageUrl = coverAsset.FullImageUrl
            });
        }

        private void AskAndMaybeEnable(Guid gameId, string gameName)
        {
            var result = _dialogs.ShowMessage(
                $"Enable Cover Shuffle for \"{gameName}\"? You can add covers to it afterwards from its right-click menu.",
                "Cover Shuffle",
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                _coverService.EnableCoverShuffle(gameId);
            }
        }
    }
}
