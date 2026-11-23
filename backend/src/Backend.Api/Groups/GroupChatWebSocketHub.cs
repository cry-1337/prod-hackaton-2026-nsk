using System.Threading.Channels;

namespace Backend.Api.Groups;

internal sealed class GroupChatWebSocketHub {
    private readonly Dictionary<Guid, HashSet<ChannelWriter<bool>>> m_Groups = [];

    public Channel<bool> Subscribe(Guid groupId) {
        var notifications = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });
        lock (m_Groups) {
            if (!m_Groups.TryGetValue(groupId, out var subscribers)) {
                subscribers = [];
                m_Groups.Add(groupId, subscribers);
            }

            subscribers.Add(notifications.Writer);
        }

        return notifications;
    }

    public void Unsubscribe(Guid groupId, ChannelWriter<bool> subscriber) {
        lock (m_Groups) {
            var subscribers = m_Groups[groupId];
            subscribers.Remove(subscriber);
            if (subscribers.Count == 0) {
                m_Groups.Remove(groupId);
            }
        }
    }

    public void Notify(Guid groupId) {
        lock (m_Groups) {
            if (m_Groups.TryGetValue(groupId, out var subscribers)) {
                foreach (var subscriber in subscribers) {
                    subscriber.TryWrite(true);
                }
            }
        }
    }
}