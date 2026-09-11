using System;
using System.Collections.Generic;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>One game <see cref="BulkShuffleService"/> could not shuffle due to an unexpected error (not an ordinary skip).</summary>
    public class BulkShuffleFailure
    {
        public Guid GameId { get; set; }

        public string GameName { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// Structured outcome of a <see cref="BulkShuffleService"/> run, so the UI
    /// can explain exactly what happened rather than a single pass/fail flag.
    /// Every game considered lands in exactly one bucket.
    /// </summary>
    public class BulkShuffleResult
    {
        /// <summary>How many games were considered. Can exceed the sum of every other field if the operation was cancelled partway through.</summary>
        public int Total { get; set; }

        public int Shuffled { get; set; }

        public int SkippedNotInstalled { get; set; }

        public int SkippedDisabled { get; set; }

        /// <summary>No enabled cover at all, or every enabled cover's file is missing - both mean "nothing usable to shuffle from".</summary>
        public int SkippedNoCovers { get; set; }

        public int Failed { get; set; }

        public List<BulkShuffleFailure> Failures { get; set; } = new List<BulkShuffleFailure>();

        public int TotalSkipped => SkippedNotInstalled + SkippedDisabled + SkippedNoCovers;

        /// <summary>How many of <see cref="Total"/> were actually processed - less than <see cref="Total"/> only if the run was cancelled partway through.</summary>
        public int Processed => Shuffled + TotalSkipped + Failed;

        public bool WasCancelled => Processed < Total;
    }

    /// <summary>Reported after each game a <see cref="BulkShuffleService"/> run finishes processing, so the UI can show live progress without freezing.</summary>
    public class BulkShuffleProgress
    {
        public int Completed { get; set; }

        public int Total { get; set; }
    }
}
