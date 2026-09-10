using System.Collections.Generic;
using System.Collections.ObjectModel;
using Playnite.SDK;

namespace PluginCoverShuffle.Tests.Fakes
{
    /// <summary>In-memory fake of <see cref="INotificationsAPI"/> so notification behavior can be tested without a real Playnite installation.</summary>
    public class FakeNotificationsApi : INotificationsAPI
    {
        private readonly ObservableCollection<NotificationMessage> _messages = new ObservableCollection<NotificationMessage>();

        public ObservableCollection<NotificationMessage> Messages => _messages;

        public int Count => _messages.Count;

        public List<(string Id, string Text, NotificationType Type)> AddedMessages { get; } = new List<(string, string, NotificationType)>();

        public void Add(NotificationMessage message)
        {
            AddedMessages.Add((message.Id, message.Text, message.Type));
            _messages.Add(message);
        }

        public void Add(string id, string text, NotificationType type)
        {
            AddedMessages.Add((id, text, type));
            _messages.Add(new NotificationMessage(id, text, type));
        }

        public void Remove(string id)
        {
            for (var i = _messages.Count - 1; i >= 0; i--)
            {
                if (_messages[i].Id == id)
                {
                    _messages.RemoveAt(i);
                }
            }
        }

        public void RemoveAll()
        {
            _messages.Clear();
        }
    }
}
