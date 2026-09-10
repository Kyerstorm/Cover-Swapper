using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Thrown when an operation would exceed <see cref="CoverLimitPolicy.MaxCoversPerGame"/>
    /// for a given game.
    /// </summary>
    public class CoverLimitExceededException : Exception
    {
        public Guid GameId { get; }

        public int Limit { get; }

        public CoverLimitExceededException(Guid gameId, int limit)
            : base($"Game '{gameId}' already has the maximum of {limit} covers.")
        {
            GameId = gameId;
            Limit = limit;
        }
    }
}
