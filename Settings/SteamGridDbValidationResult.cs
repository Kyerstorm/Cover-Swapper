namespace PluginCoverShuffle.Settings
{
    /// <summary>Outcome of validating a SteamGridDB API key from the settings UI.</summary>
    public class SteamGridDbValidationResult
    {
        public bool IsValid { get; }

        public string Message { get; }

        public SteamGridDbValidationResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }
    }
}
