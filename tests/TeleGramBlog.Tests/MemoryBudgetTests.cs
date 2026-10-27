using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class MemoryBudgetTests
{
    [Fact] public void DormantTopicHistoriesHaveCountAndMessageBudgets()
    {
        var cache=new HistoryPagerCache(2,5);var first=Pager(2);var second=Pager(2);var third=Pager(2);
        cache[(1,1)]=first;cache[(1,2)]=second;Assert.Same(first,cache.GetValueOrDefault((1,1)));
        cache[(1,3)]=third;Assert.Null(cache.GetValueOrDefault((1,2)));Assert.Same(first,cache.GetValueOrDefault((1,1)));
        cache[(1,4)]=Pager(6);Assert.Null(cache.GetValueOrDefault((1,4)));Assert.True(cache.Count<=2);
        cache.Clear();Assert.Equal(0,cache.Count);
    }
    static HistoryPager Pager(int count)=>new(new OneBatch(0),new HistorySnapshot(Enumerable.Range(1,count).Select(i=>new HistoryMessage(i,0,DateTime.UtcNow,"",[])).ToList(),1,true));
    [Fact] public async Task FailedPartialHistoryCannotRemainOverBudgetWhenLeavingTopic()
    {
        var cache=new HistoryPagerCache(8,5);var pager=new HistoryPager(new GrowingFailure());cache[(1,1)]=pager;
        await Assert.ThrowsAsync<IOException>(()=>pager.GetPageAsync(20,1));Assert.Equal(10,pager.LoadedMessageCount);
        cache.Trim();Assert.Null(cache.GetValueOrDefault((1,1)));Assert.Equal(0,cache.Count);
    }
    sealed class GrowingFailure:IHistorySource
    {public Task<HistoryBatch> FetchAsync(int before,CancellationToken ct)=>before!=0?throw new IOException("offline"):Task.FromResult(new HistoryBatch(Enumerable.Range(91,10).Select(i=>new HistoryMessage(i,0,DateTime.UtcNow,"",[],new MediaRef(1,i,"photo"))).ToArray(),91,false));}
    [Fact] public async Task RepeatedLoadedCountReadsDoNotRebuildAllPostObjects()
    {
        var pager=new HistoryPager(new OneBatch(3000));await pager.GetPageAsync(1,24);
        _=pager.LoadedCards.Count;
        var start=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<100;i++)Assert.Equal(3000,pager.LoadedCards.Count);
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-start<100_000,"Reading a loaded count should not allocate a new album index each time.");
    }
    [Fact] public async Task AlbumIndexInvalidatesWhenMoreHistoryArrives()
    {
        var source=new TwoBatches();var pager=new HistoryPager(source);
        var first=await pager.GetPageAsync(1,1);Assert.Equal(2,first.LoadedCount);
        var next=await pager.GetPageAsync(3,1);Assert.Equal(4,next.LoadedCount);Assert.Equal(4,pager.LoadedCards.Count);
    }
    sealed class OneBatch(int count):IHistorySource
    {public Task<HistoryBatch> FetchAsync(int before,CancellationToken ct)=>Task.FromResult(new HistoryBatch(Enumerable.Range(1,count).Select(i=>new HistoryMessage(i,0,DateTime.UtcNow,"Caption",[],new MediaRef(1,i,"photo"))).ToArray(),1,true));}
    sealed class TwoBatches:IHistorySource
    {public Task<HistoryBatch> FetchAsync(int before,CancellationToken ct)=>Task.FromResult(new HistoryBatch((before==0?new[]{4,3}:new[]{2,1}).Select(i=>new HistoryMessage(i,0,DateTime.UtcNow,"Caption",[],new MediaRef(1,i,"photo"))).ToArray(),before==0?3:1,before!=0));}
}
