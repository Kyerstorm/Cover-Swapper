using System.Collections.Generic;

namespace PluginCoverShuffle.Infrastructure.Providers.SteamGridDb
{
    /// <summary>Shape of every SteamGridDB API v2 JSON response.</summary>
    internal class SteamGridDbEnvelope<T>
    {
        public bool Success { get; set; }

        public T Data { get; set; }

        public List<string> Errors { get; set; }
    }
}
