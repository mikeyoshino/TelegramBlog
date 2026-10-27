using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class PagingTests
{
    static HistoryMessage M(int id, long album = 0, bool eligible = true) => new(id, album, DateTime.UnixEpoch.AddSeconds(id), "post " + id, [], eligible ? new(8, id, "photo") : null);
    [Fact] public async Task AlbumAcrossBatchesIsOneCompleteCard()
    {
        var source = new Source(new([M(10, 9)], 10, false), new([M(9, 9), M(8)], 8, true));
        var page = await new HistoryPager(source).GetPageAsync(1, 24);
        Assert.Equal(2, page.Cards.Count); Assert.Equal(2, page.Cards[0].Media.Count); Assert.False(page.HasNext);
    }
    [Fact] public async Task SparseHistoryFillsPageAndDeduplicates()
    {
        var source = new Source(new([M(10), M(9, eligible:false)], 9, false), new([M(10), M(8), M(7)], 7, true));
        var pager = new HistoryPager(source);
        var page = await pager.GetPageAsync(1, 2);
        Assert.Equal(new[] {10,8}, page.Cards.Select(c=>c.Id)); Assert.True(page.HasNext);
        Assert.Equal(7, (await pager.GetPageAsync(2,2)).Cards.Single().Id);
    }
    [Fact] public async Task EmptyHistoryIsOneEmptyPage()
    {
        var p = await new HistoryPager(new Source(new HistoryBatch([],0,true))).GetPageAsync(1,24);
        Assert.Empty(p.Cards); Assert.Equal(1,p.KnownPages); Assert.False(p.HasNext);
    }
    [Fact] public async Task StuckCursorDoesNotLoopForever()
    {
        var pager = new HistoryPager(new Source(new([M(10)],10,false),new([M(10)],10,false)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>pager.GetPageAsync(1,24));
    }
    [Fact] public async Task CanceledResultIsNotCommitted()
    {
        using var cts = new CancellationTokenSource();
        var pager = new HistoryPager(new CancelSource(cts));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pager.GetPageAsync(1,24,cts.Token));
    }
    sealed class CancelSource(CancellationTokenSource cts):IHistorySource
    {
        public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken ct) { cts.Cancel(); return Task.FromResult(new HistoryBatch([M(10)],10,true)); }
    }
    sealed class Source(params HistoryBatch[] batches):IHistorySource
    {
        int index;
        public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken ct)=>Task.FromResult(batches[index++]);
    }
}
