using System;
using Playnite.SDK;
using PluginCoverShuffle.Domain;

namespace PluginCoverShuffle.Playnite.Integration
{
    /// <summary>
    /// Surfaces shuffle activity as Playnite notifications, respecting each
    /// game's effective <see cref="NotificationPreference"/>. The only class
    /// that touches <see cref="INotificationsAPI"/>.
    /// </summary>
    public class CoverShuffleNotificationService
    {
        private readonly INotificationsAPI _notifications;

        public CoverShuffleNotificationService(INotificationsAPI notifications)
        {
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        /// <summary>
        /// A game's own notification id, reused across calls: a later
        /// shuffle for the same game replaces its previous notification
        /// instead of piling up unread ones.
        /// </summary>
        private static string NotificationId(Guid gameId) => "coverShuffle-" + gameId.ToString("N");

        public void NotifyShuffled(Guid gameId, string gameName, NotificationPreference preference)
        {
            if (preference != NotificationPreference.NotifyOnShuffle)
            {
                return;
            }

            _notifications.Add(NotificationId(gameId), $"Cover Shuffle rotated the cover for \"{gameName}\".", NotificationType.Info);
        }

        public void NotifyShuffleFailed(Guid gameId, string gameName, string message, NotificationPreference preference)
        {
            if (preference == NotificationPreference.Silent)
            {
                return;
            }

            _notifications.Add(NotificationId(gameId), $"Cover Shuffle could not shuffle \"{gameName}\": {message}", NotificationType.Error);
        }
    }
}
