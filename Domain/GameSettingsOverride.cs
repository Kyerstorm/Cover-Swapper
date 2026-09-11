using System;

namespace PluginCoverShuffle.Domain
{
    /// <summary>
    /// Sparse per-game override of the global <see cref="CoverShuffleSettings"/>
    /// defaults. Every field is nullable: <c>null</c> means "inherit the
    /// current global value," a set value means "use this instead, live,
    /// regardless of future global changes to this field." A game can
    /// therefore override just one setting (e.g. its interval) while every
    /// other setting keeps tracking global changes.
    /// </summary>
    public class GameSettingsOverride
    {
        public GameSettingsOverride()
        {
        }

        /// <summary>Creates an independent copy of <paramref name="source"/>.</summary>
        public GameSettingsOverride(GameSettingsOverride source)
        {
            Enabled = source.Enabled;
            Interval = source.Interval;
            Mode = source.Mode;
            AvoidConsecutiveDuplicates = source.AvoidConsecutiveDuplicates;
            ShuffleOnStartup = source.ShuffleOnStartup;
            ShuffleOnGameLaunch = source.ShuffleOnGameLaunch;
            NotificationPreference = source.NotificationPreference;
            NewGameBehavior = source.NewGameBehavior;
        }

        public bool? Enabled { get; set; }

        public TimeSpan? Interval { get; set; }

        public ShuffleMode? Mode { get; set; }

        public bool? AvoidConsecutiveDuplicates { get; set; }

        public bool? ShuffleOnStartup { get; set; }

        public bool? ShuffleOnGameLaunch { get; set; }

        public NotificationPreference? NotificationPreference { get; set; }

        public NewGameBehavior? NewGameBehavior { get; set; }
    }
}
