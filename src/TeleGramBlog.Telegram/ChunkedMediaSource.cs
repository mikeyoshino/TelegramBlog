namespace TeleGramBlog.Telegram;

/// <summary>Bounded, seekable media cache. Requests only blocks touched by the decoder.</summary>
public sealed class ChunkedMediaSource : IDisposable
{
    public const int ChunkSize=256*1024;
    readonly Func<long,int,CancellationToken,Task<byte[]>> fetch;
    readonly CancellationTokenSource lifetime;
    readonly CancellationToken lifetimeToken;
    readonly SemaphoreSlim gate=new(1);
    readonly Dictionary<long,(byte[] Bytes,LinkedListNode<long> Node)> cache=[];
    readonly LinkedList<long> recent=new();
    readonly int cacheChunks;
    int disposed;
    public long Length {get;}
    public string ContentType {get;}
    public ChunkedMediaSource(long length,string contentType,Func<long,int,CancellationToken,Task<byte[]>> fetch,int cacheChunks=64,CancellationToken cancellationToken=default)
    {
        if(length<=0)throw new ArgumentOutOfRangeException(nameof(length));
        if(cacheChunks<1)throw new ArgumentOutOfRangeException(nameof(cacheChunks));
        Length=length;ContentType=contentType;this.fetch=fetch;this.cacheChunks=cacheChunks;
        lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lifetimeToken=lifetime.Token;
    }
    public async Task<int> ReadAtAsync(long position,Memory<byte> destination,CancellationToken ct)
    {
        if(position<0)throw new ArgumentOutOfRangeException(nameof(position));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetimeToken);ct=linked.Token;
        ct.ThrowIfCancellationRequested();
        var total=0;
        while(total<destination.Length&&position<Length)
        {
            var offset=position/ChunkSize*ChunkSize;
            var block=await GetBlockAsync(offset,ct);
            var within=(int)(position-offset);
            var count=Math.Min(destination.Length-total,block.Length-within);
            if(count<=0)throw new EndOfStreamException("Incomplete media block.");
            block.AsMemory(within,count).CopyTo(destination[total..]);total+=count;position+=count;
        }
        return total;
    }
    async Task<byte[]> GetBlockAsync(long offset,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            if(cache.TryGetValue(offset,out var entry)){recent.Remove(entry.Node);recent.AddLast(entry.Node);return entry.Bytes;}
            var bytes=await fetch(offset,ChunkSize,ct);
            ct.ThrowIfCancellationRequested();
            var expected=(int)Math.Min(ChunkSize,Length-offset);
            if(bytes.Length!=expected)throw new EndOfStreamException("Incomplete media block.");
            if(cache.Count==cacheChunks){var oldest=recent.First!;cache.Remove(oldest.Value);recent.RemoveFirst();}
            cache[offset]=(bytes,recent.AddLast(offset));return bytes;
        }
        finally{gate.Release();}
    }
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        lifetime.Cancel();
        _=ClearAsync();
    }
    async Task ClearAsync()
    {
        await gate.WaitAsync();
        try{cache.Clear();recent.Clear();lifetime.Dispose();}finally{gate.Release();}
    }
}
