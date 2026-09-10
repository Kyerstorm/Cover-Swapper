namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>Outcome of a raw <see cref="SteamGridDbClient"/> call.</summary>
    internal class SteamGridDbResult<T>
    {
        public bool Success { get; private set; }

        public T Value { get; private set; }

        public SteamGridDbErrorKind ErrorKind { get; private set; }

        /// <summary>User-facing explanation; never contains the API key.</summary>
        public string ErrorMessage { get; private set; }

        public static SteamGridDbResult<T> Ok(T value)
        {
            return new SteamGridDbResult<T> { Success = true, Value = value, ErrorKind = SteamGridDbErrorKind.None };
        }

        public static SteamGridDbResult<T> Failed(SteamGridDbErrorKind kind, string errorMessage)
        {
            return new SteamGridDbResult<T> { Success = false, ErrorKind = kind, ErrorMessage = errorMessage };
        }
    }
}
