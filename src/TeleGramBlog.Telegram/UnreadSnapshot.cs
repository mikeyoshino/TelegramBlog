using TL;
namespace TeleGramBlog.Telegram;

// A forum total is valid only after all unique, full topic states have arrived.
public sealed class UnreadSnapshot
{
    readonly Dictionary<int,int> counts = [];
    public int TopicCount => counts.Count;
    public static int Count(int unread, int readThrough, int topMessage)
        => topMessage > 0 && readThrough >= topMessage ? 0 : Math.Max(0,unread);
    public void Add(IEnumerable<ForumTopic> topics)
    {
        foreach(var topic in topics)
        {
            if(topic.flags.HasFlag(ForumTopic.Flags.short_)) throw new IOException("Telegram returned incomplete topic read states. Retry refreshing groups.");
            counts[topic.id] = Count(topic.unread_count,topic.read_inbox_max_id,topic.top_message);
        }
    }
    public int Complete(int expectedTopics)
    {
        if(counts.Count != expectedTopics) throw new IOException("Topic read states are incomplete or changed during refresh. Retry refreshing groups.");
        return counts.Values.Sum();
    }
}
