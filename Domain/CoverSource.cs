using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Identifies where a <see cref="Cover"/> originated from.
    /// </summary>
    public enum CoverSource
    {
        SteamGridDb,
        PlayniteMetadata,
        LocalFile
    }
}
