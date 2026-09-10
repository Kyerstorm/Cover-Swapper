namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    internal enum SteamGridDbErrorKind
    {
        None,
        MissingApiKey,
        InvalidApiKey,
        NetworkError,
        UnexpectedResponse
    }
}
