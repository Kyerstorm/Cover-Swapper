namespace PluginCoverShuffle.Domain.Shuffling
{
    /// <summary>How the current cover in a <see cref="ShuffleState"/> came to be selected.</summary>
    public enum ShuffleTrigger
    {
        /// <summary>Selected by the randomized <see cref="IShuffleEngine"/> cycle.</summary>
        Random,

        /// <summary>Explicitly picked by the user, overriding the randomized cycle.</summary>
        Manual
    }
}
