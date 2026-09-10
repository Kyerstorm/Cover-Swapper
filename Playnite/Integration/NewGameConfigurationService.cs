using System;
using System.Windows;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Applies the user's configured <see cref="NewGameBehavior"/> to a game
    /// Cover Shuffle has determined is new to it. Never contacts a network
    /// provider itself, regardless of behaviour — "Automatic" only enables
    /// shuffling and captures the original cover so the game is ready to
    /// have covers added to it later.
    /// </summary>
    public class NewGameConfigurationService
    {
        private readonly PlayniteCoverService _coverService;
        private readonly Func<CoverShuffleSettings> _globalSettingsProvider;
        private readonly IDialogsFactory _dialogs;
        private readonly ICoverShuffleLogger _logger;

        public NewGameConfigurationService(
            PlayniteCoverService coverService,
            Func<CoverShuffleSettings> globalSettingsProvider,
            IDialogsFactory dialogs,
            ICoverShuffleLogger logger)
        {
            _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
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
                    _coverService.EnableCoverShuffle(gameId);
                    _logger.Info($"Automatically prepared new game '{gameId}' for Cover Shuffle.");
                    return;
            }
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
