using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Infrastructure.Logging;

namespace PluginCoverShuffle.Settings
{
    /// <summary>
    /// Settings view model bridging Playnite's <see cref="ISettings"/> contract
    /// to the plugin's <see cref="CoverShuffleSettings"/> domain model.
    /// UI code binds to this class; it must not touch persistence or file
    /// storage directly.
    /// </summary>
    public class CoverShufflePluginSettingsViewModel : ObservableObject, ISettings
    {
        private readonly Plugin _plugin;
        private readonly ICoverShuffleLogger _logger;
        private readonly Func<string, Task<SteamGridDbValidationResult>> _validateApiKey;
        private CoverShuffleSettings _editingClone;
        private CoverShuffleSettings _settings;
        private string _apiKeyValidationStatus;
        private bool _isValidatingApiKey;

        public CoverShuffleSettings Settings
        {
            get => _settings;
            set => SetValue(ref _settings, value);
        }

        /// <summary>Result text of the last "Validate" click; null until one has run.</summary>
        public string ApiKeyValidationStatus
        {
            get => _apiKeyValidationStatus;
            set => SetValue(ref _apiKeyValidationStatus, value);
        }

        public bool IsValidatingApiKey
        {
            get => _isValidatingApiKey;
            set => SetValue(ref _isValidatingApiKey, value);
        }

        public CoverShufflePluginSettingsViewModel(
            Plugin plugin,
            ICoverShuffleLogger logger,
            Func<string, Task<SteamGridDbValidationResult>> validateApiKey)
        {
            _plugin = plugin;
            _logger = logger;
            _validateApiKey = validateApiKey ?? throw new ArgumentNullException(nameof(validateApiKey));

            var savedSettings = plugin.LoadPluginSettings<CoverShuffleSettings>();
            Settings = savedSettings ?? new CoverShuffleSettings();
        }

        /// <summary>Checks the currently entered (not-yet-saved) API key against SteamGridDB.</summary>
        public async Task ValidateApiKeyAsync()
        {
            if (string.IsNullOrWhiteSpace(Settings.SteamGridDbApiKey))
            {
                ApiKeyValidationStatus = "Enter an API key first.";
                return;
            }

            IsValidatingApiKey = true;
            ApiKeyValidationStatus = "Checking...";
            try
            {
                var result = await _validateApiKey(Settings.SteamGridDbApiKey).ConfigureAwait(true);
                ApiKeyValidationStatus = result.IsValid ? "API key is valid." : result.Message;
            }
            finally
            {
                IsValidatingApiKey = false;
            }
        }

        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(Settings);
            _logger.Info("Cover Shuffle settings saved.");
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (Settings.Interval.TotalMinutes < 1)
            {
                errors.Add("Shuffle interval must be at least 1 minute.");
            }

            return errors.Count == 0;
        }
    }
}
