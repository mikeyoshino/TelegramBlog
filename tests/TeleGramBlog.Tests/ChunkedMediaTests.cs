using TeleGramBlog.Telegram;
namespace TeleGramBlog.Tests;
public class ChunkedMediaTests
{
    [Fact] public async Task SeekingNearEndDoesNotDownloadEarlierContentAndReusesCache()
    {
        var offsets=new List<long>();
        using var source=new ChunkedMediaSource(100L*1024*1024,"video/mp4",(offset,count,ct)=>{offsets.Add(offset);return Task.FromResult(Enumerable.Repeat((byte)(offset/ChunkedMediaSource.ChunkSize),count).ToArray());});
        var buffer=new byte[16];
        var position=source.Length-100;
        Assert.Equal(16,await source.ReadAtAsync(position,buffer,default));
        Assert.Single(offsets);Assert.Equal(position/ChunkedMediaSource.ChunkSize*ChunkedMediaSource.ChunkSize,offsets[0]);
        await source.ReadAtAsync(position,buffer,default);Assert.Single(offsets);
    }
    [Fact] public async Task CrossChunkAndFinalReadsReturnExactBytes()
    {
        var bytes=Enumerable.Range(0,ChunkedMediaSource.ChunkSize+7).Select(i=>(byte)(i%251)).ToArray();
        using var source=new ChunkedMediaSource(bytes.Length,"video/mp4",(offset,count,ct)=>Task.FromResult(bytes.Skip((int)offset).Take(count).ToArray()));
        var buffer=new byte[20];var offset=ChunkedMediaSource.ChunkSize-5;
        Assert.Equal(12,await source.ReadAtAsync(offset,buffer,default));
        Assert.Equal(bytes.Skip(offset),buffer.Take(12));
        Assert.Equal(0,await source.ReadAtAsync(bytes.Length,buffer,default));
    }
    [Fact] public async Task CancelAndDisposeStopPendingReads()
    {
        var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source=new ChunkedMediaSource(1024,"video/mp4",async(offset,count,ct)=>{started.SetResult();await Task.Delay(Timeout.Infinite,ct);return [];});
        var read=source.ReadAtAsync(0,new byte[10],default);await started.Task;source.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>read);
    }
    [Fact] public async Task CacheIsBoundedAndFailuresCanRetry()
    {
        var calls=0;
        using var source=new ChunkedMediaSource(100L*ChunkedMediaSource.ChunkSize,"video/mp4",(offset,count,ct)=>{
            if(++calls==1)throw new IOException("offline");return Task.FromResult(new byte[count]);
        },cacheChunks:2);
        await Assert.ThrowsAsync<IOException>(()=>source.ReadAtAsync(0,new byte[1],default));
        for(var i=0;i<3;i++)await source.ReadAtAsync(i*(long)ChunkedMediaSource.ChunkSize,new byte[1],default);
        await source.ReadAtAsync(0,new byte[1],default);
        Assert.Equal(5,calls);
    }
    [Fact] public async Task ConcurrentDecoderClonesShareCachedBlock()
    {
        var calls=0;
        using var source=new ChunkedMediaSource(ChunkedMediaSource.ChunkSize,"video/mp4",async(offset,count,ct)=>{
            Interlocked.Increment(ref calls);await Task.Delay(20,ct);return new byte[count];
        });
        await Task.WhenAll(Enumerable.Range(0,8).Select(i=>source.ReadAtAsync(i,new byte[32],default)));
        Assert.Equal(1,calls);
    }
    [Fact] public async Task DisposedSourceRejectsFurtherReadsAndTruncatedBlocksAreNotCached()
    {
        var calls=0;
        var source=new ChunkedMediaSource(1024,"video/mp4",(offset,count,ct)=>Task.FromResult(new byte[++calls==1?100:1024]));
        await Assert.ThrowsAsync<EndOfStreamException>(()=>source.ReadAtAsync(0,new byte[10],default));
        Assert.Equal(10,await source.ReadAtAsync(0,new byte[10],default));
        source.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>source.ReadAtAsync(0,new byte[10],default));
    }
}
