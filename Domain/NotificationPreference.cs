namespace PluginCoverShuffle.Domain
{
    /// <summary>Controls how much the plugin communicates shuffle activity to the user.</summary>
    public enum NotificationPreference
    {
        Silent,
        NotifyOnShuffle,
        NotifyOnErrorOnly
    }
}
