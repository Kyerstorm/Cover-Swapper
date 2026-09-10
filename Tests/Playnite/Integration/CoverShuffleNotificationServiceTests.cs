using System;
using Playnite.SDK;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.Playnite.Integration;
using PluginCoverShuffle.Tests.Fakes;
using Xunit;

namespace PluginCoverShuffle.Tests.Playnite.Integration
{
    public class CoverShuffleNotificationServiceTests
    {
        private readonly FakeNotificationsApi _notifications = new FakeNotificationsApi();
        private readonly CoverShuffleNotificationService _service;

        public CoverShuffleNotificationServiceTests()
        {
            _service = new CoverShuffleNotificationService(_notifications);
        }

        [Theory]
        [InlineData(NotificationPreference.NotifyOnShuffle)]
        public void NotifyShuffled_WithNotifyOnShuffle_AddsInfoNotification(NotificationPreference preference)
        {
            var gameId = Guid.NewGuid();

            _service.NotifyShuffled(gameId, "Some Game", preference);

            Assert.Single(_notifications.AddedMessages);
            Assert.Equal(NotificationType.Info, _notifications.AddedMessages[0].Type);
        }

        [Theory]
        [InlineData(NotificationPreference.Silent)]
        [InlineData(NotificationPreference.NotifyOnErrorOnly)]
        public void NotifyShuffled_WithNonShuffleNotifyingPreference_AddsNothing(NotificationPreference preference)
        {
            _service.NotifyShuffled(Guid.NewGuid(), "Some Game", preference);

            Assert.Empty(_notifications.AddedMessages);
        }

        [Theory]
        [InlineData(NotificationPreference.NotifyOnShuffle)]
        [InlineData(NotificationPreference.NotifyOnErrorOnly)]
        public void NotifyShuffleFailed_WhenNotSilent_AddsErrorNotification(NotificationPreference preference)
        {
            _service.NotifyShuffleFailed(Guid.NewGuid(), "Some Game", "no covers", preference);

            Assert.Single(_notifications.AddedMessages);
            Assert.Equal(NotificationType.Error, _notifications.AddedMessages[0].Type);
        }

        [Fact]
        public void NotifyShuffleFailed_WhenSilent_AddsNothing()
        {
            _service.NotifyShuffleFailed(Guid.NewGuid(), "Some Game", "no covers", NotificationPreference.Silent);

            Assert.Empty(_notifications.AddedMessages);
        }

        [Fact]
        public void NotifyShuffled_CalledTwiceForSameGame_ReusesTheSameNotificationId()
        {
            var gameId = Guid.NewGuid();

            _service.NotifyShuffled(gameId, "Some Game", NotificationPreference.NotifyOnShuffle);
            _service.NotifyShuffled(gameId, "Some Game", NotificationPreference.NotifyOnShuffle);

            Assert.Equal(2, _notifications.AddedMessages.Count);
            Assert.Equal(_notifications.AddedMessages[0].Id, _notifications.AddedMessages[1].Id);
        }
    }
}
