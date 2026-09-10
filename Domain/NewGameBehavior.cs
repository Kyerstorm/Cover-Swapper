namespace PluginCoverShuffle.Domain
{
    /// <summary>What Cover Shuffle should do when a game it hasn't seen before is installed.</summary>
    public enum NewGameBehavior
    {
        /// <summary>Never touch a newly installed game automatically.</summary>
        DoNothing,

        /// <summary>Ask the user whether to enable Cover Shuffle for the new game.</summary>
        Ask,

        /// <summary>
        /// Automatically enable Cover Shuffle for the new game (captures the
        /// original cover, marks it enabled). Never contacts a network
        /// provider on its own — that always requires a separate, explicit
        /// user action.
        /// </summary>
        Automatic
    }
}
