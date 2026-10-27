namespace TeleGramBlog.Core;
// Active history remains owned by the caller. This only budgets remembered topics.
public sealed class HistoryPagerCache(int maximumTopics=8,int maximumMessages=20_000)
{
    readonly Dictionary<(long,int),HistoryPager> entries=[];
    readonly LinkedList<(long,int)> recent=new();
    public int Count=>entries.Count;
    public HistoryPager? GetValueOrDefault((long,int) key)
    {
        Trim();
        if(!entries.TryGetValue(key,out var pager))return null;
        recent.Remove(key);recent.AddLast(key);return pager;
    }
    public HistoryPager this[(long,int) key]
    {
        set
        {
            entries.Remove(key);recent.Remove(key);
            // An oversized active topic must not evict all useful small histories.
            if(value.LoadedMessageCount>maximumMessages)return;
            entries[key]=value;recent.AddLast(key);
            Trim();
        }
    }
    public void Trim()
    {
        foreach(var key in entries.Where(e=>e.Value.LoadedMessageCount>maximumMessages).Select(e=>e.Key).ToArray()){entries.Remove(key);recent.Remove(key);}
        while(entries.Count>maximumTopics||entries.Values.Sum(p=>p.LoadedMessageCount)>maximumMessages)
        {var oldest=recent.First!.Value;recent.RemoveFirst();entries.Remove(oldest);}
    }
    public void Clear(){entries.Clear();recent.Clear();}
}
