namespace TeleGramBlog.Core;
public sealed class HistoryPager(IHistorySource source, HistorySnapshot? snapshot = null, bool includeText = false, bool mediaOnly = false)
{
    readonly SemaphoreSlim gate = new(1);
    readonly Dictionary<int, HistoryMessage> messages = (snapshot?.Messages ?? []).ToDictionary(m=>m.Id);
    int beforeId = snapshot?.BeforeId ?? 0;
    bool ended = snapshot?.EndReached ?? false;
    DateTimeOffset? fetchedAt=snapshot?.FetchedAt;
    List<PostCard>? builtCards;
    public DateTimeOffset? FetchedAt=>fetchedAt;
    public int LoadedMessageCount=>messages.Count;
    public HistorySnapshot Snapshot => new(messages.Values.OrderByDescending(m=>m.Id).ToList(), beforeId, ended,fetchedAt);
    public IReadOnlyList<PostCard> LoadedCards => BuildCards();
    public PostPage GetCachedPage(int page,int pageSize)
    {
        if(page<1 || pageSize<1 || pageSize>100)throw new ArgumentOutOfRangeException(nameof(page));
        var cards=BuildCards();
        return new(cards.Skip((page-1)*pageSize).Take(pageSize).ToArray(),page,Math.Max(1,(cards.Count+pageSize-1)/pageSize),!ended||cards.Count>page*pageSize,ended,cards.Count);
    }

    public async Task<PostPage> GetPageAsync(int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100) throw new ArgumentOutOfRangeException(nameof(page));
        await gate.WaitAsync(ct);
        try
        {
            var cards = BuildCards();
            while (!ended && cards.Count <= checked(page * pageSize))
            {
                var batch = await source.FetchAsync(beforeId, ct);
                ct.ThrowIfCancellationRequested();
                if (!batch.EndReached && (batch.NextBeforeId <= 0 || (beforeId > 0 && batch.NextBeforeId >= beforeId)))
                    throw new InvalidOperationException("Telegram history did not advance. Refresh and try again.");
                foreach (var message in batch.Messages) messages[message.Id] = message;
                beforeId = batch.NextBeforeId;
                ended = batch.EndReached;
                fetchedAt=DateTimeOffset.UtcNow;
                builtCards=null;
                cards = BuildCards();
            }
            return new(cards.Skip((page-1)*pageSize).Take(pageSize).ToArray(), page,
                Math.Max(1,(cards.Count+pageSize-1)/pageSize), !ended || cards.Count > page*pageSize, ended, cards.Count);
        }
        finally { gate.Release(); }
    }
    List<PostCard> BuildCards()
    {
        if(builtCards!=null)return builtCards;
        var ordered = messages.Values.OrderByDescending(m=>m.Id).ToList();
        var pendingAlbum = !ended && ordered.Count > 0 ? ordered[^1].AlbumId : 0;
        return builtCards=ordered.Where(m=>pendingAlbum == 0 || m.AlbumId != pendingAlbum)
            .GroupBy(m=>m.AlbumId == 0 ? $"m{m.Id}" : $"a{m.AlbumId}")
            .Select(g=>new PostCard(g.First().Id,g.First().Date,
                string.Join("\n",g.Select(m=>m.Text).Where(t=>!string.IsNullOrWhiteSpace(t)).Distinct()),
                g.SelectMany(m=>m.Links).Distinct().ToArray(),g.Where(m=>m.Media != null).OrderBy(m=>m.Id).Select(m=>m.Media!).ToArray()))
            .Where(p=>p.Media.Count>0 || !mediaOnly && (p.Links.Count>0 || includeText && !string.IsNullOrWhiteSpace(p.Text))).ToList();
    }
}
