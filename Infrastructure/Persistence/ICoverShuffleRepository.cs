using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Infrastructure.Persistence
{
    /// <summary>
    /// Persists and retrieves all plugin-owned state: per-game configuration,
    /// cover records, shuffle state, and original artwork restoration info.
    /// Implementations must survive process restarts.
    /// </summary>
    public interface ICoverShuffleRepository
    {
        /// <summary>Returns the game's configuration, or null if none has been saved.</summary>
        GameConfiguration GetGameConfiguration(Guid gameId);

        /// <summary>Creates or replaces the game's configuration.</summary>
        void SaveGameConfiguration(GameConfiguration configuration);

        /// <summary>
        /// Returns every game configuration Cover Shuffle has ever saved,
        /// regardless of enabled state. Used at startup to find games that
        /// might have a shuffle due, without scanning Playnite's whole library.
        /// </summary>
        IReadOnlyList<GameConfiguration> GetAllGameConfigurations();

        /// <summary>Returns the distinct game IDs that have at least one stored cover, even if never explicitly configured.</summary>
        IReadOnlyList<Guid> GetGameIdsWithCovers();

        /// <summary>Returns all covers stored for a game, in the order they were added.</summary>
        IReadOnlyList<Cover> GetCovers(Guid gameId);

        /// <summary>Returns a single cover, or null if it does not exist.</summary>
        Cover GetCover(Guid gameId, Guid coverId);

        /// <summary>
        /// Adds a new cover record for a game. Throws <see cref="CoverLimitExceededException"/>
        /// if the game already has <see cref="CoverLimitPolicy.MaxCoversPerGame"/> covers.
        /// </summary>
        void AddCover(Cover cover);

        /// <summary>
        /// Removes a cover record for a game. Does not delete the physical cover
        /// file; callers must do that deliberately via <see cref="Storage.ICoverStorage"/>.
        /// </summary>
        void RemoveCover(Guid gameId, Guid coverId);

        /// <summary>Replaces an existing cover record with updated values.</summary>
        void UpdateCover(Cover cover);

        /// <summary>Returns the game's shuffle state, or null if none has been saved.</summary>
        ShuffleState GetShuffleState(Guid gameId);

        /// <summary>Creates or replaces the game's shuffle state.</summary>
        void SaveShuffleState(ShuffleState state);

        /// <summary>Returns the game's captured original artwork info, or null if none exists.</summary>
        OriginalArtworkInfo GetOriginalArtwork(Guid gameId);

        /// <summary>Creates or replaces the game's original artwork info.</summary>
        void SaveOriginalArtwork(OriginalArtworkInfo info);

        /// <summary>Removes the game's original artwork info, e.g. after a restore completes.</summary>
        void ClearOriginalArtwork(Guid gameId);
    }
}
