using System;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Narrow abstraction over the parts of Playnite's game database that
    /// Cover Shuffle needs. Kept separate from <c>IPlayniteAPI</c> itself so
    /// that application logic can be unit tested with a fake implementation
    /// instead of depending on a live Playnite installation.
    /// </summary>
    public interface IPlayniteGameService
    {
        /// <summary>
        /// Returns the game's current cover reference as understood by
        /// Playnite, or null if the game cannot be found. Never assume this
        /// is a plain local file path.
        /// </summary>
        string GetCoverReference(Guid gameId);

        /// <summary>
        /// Sets the game's cover reference and persists the change to
        /// Playnite's database. Does nothing if the game cannot be found.
        /// </summary>
        void SetCoverReference(Guid gameId, string coverReference);

        /// <summary>Returns the game's display name, or null if it cannot be found.</summary>
        string GetGameName(Guid gameId);

        /// <summary>Whether the game is currently installed. False if the game cannot be found.</summary>
        bool IsGameInstalled(Guid gameId);
    }
}
