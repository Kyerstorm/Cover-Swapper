namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Central definition of the per-game cover limit. All code that needs
    /// to enforce or display the limit must reference this policy rather
    /// than hard-coding the value.
    /// </summary>
    public static class CoverLimitPolicy
    {
        /// <summary>Maximum number of covers that may be stored for a single game.</summary>
        public const int MaxCoversPerGame = 10;
    }
}
