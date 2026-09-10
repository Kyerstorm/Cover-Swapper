namespace PluginCoverShuffle.Domain
{
    /// <summary>Determines how the shuffle engine advances to the next cover.</summary>
    public enum ShuffleMode
    {
        /// <summary>Shuffle on a fixed time interval.</summary>
        Interval,

        /// <summary>Shuffle each time the game is launched.</summary>
        OnGameLaunch,

        /// <summary>Shuffle only when the user triggers it manually.</summary>
        Manual
    }
}
