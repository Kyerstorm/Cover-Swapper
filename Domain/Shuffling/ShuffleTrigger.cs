namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>How the current cover in a <see cref="ShuffleState"/> came to be selected.</summary>
    public enum ShuffleTrigger
    {
        /// <summary>Selected by the randomized <see cref="IShuffleEngine"/> cycle.</summary>
        Random,

        /// <summary>Explicitly picked by the user, overriding the randomized cycle.</summary>
        Manual,

        /// <summary>
        /// The game's very first usable cover, applied automatically the
        /// moment Cover Shuffle has one to show (see
        /// <see cref="PluginCoverShuffle.Playnite.Integration.PlayniteCoverService.TryApplyInitialShuffle"/>).
        /// Kept distinct from <see cref="Random"/> and <see cref="Manual"/>
        /// so history/statistics can tell "the game's starting cover" apart
        /// from an ordinary shuffle or an explicit user pick.
        /// </summary>
        Initial
    }
}
