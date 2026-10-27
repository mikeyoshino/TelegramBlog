using System.Runtime.InteropServices.WindowsRuntime;
using TeleGramBlog.Telegram;
using Windows.Foundation;
using Windows.Storage.Streams;
namespace TeleGramBlog.App.WinUI;

/// <summary>Each decoder clone has its own cursor and shares the bounded byte cache.</summary>
public sealed class VideoRandomAccessStream:IRandomAccessStream
{
    readonly CancellationTokenSource lifetime=new();
    readonly CancellationToken lifetimeToken;
    readonly ChunkedMediaSource source;
    ulong position;
    int disposed;
    public VideoRandomAccessStream(ChunkedMediaSource source,ulong position=0){this.source=source;this.position=position;lifetimeToken=lifetime.Token;}
    public bool CanRead=>true;
    public bool CanWrite=>false;
    public ulong Position=>position;
    public ulong Size {get=>(ulong)source.Length;set=>throw new NotSupportedException();}
    public void Seek(ulong value){if(value>Size)throw new ArgumentOutOfRangeException(nameof(value));position=value;}
    public IRandomAccessStream CloneStream()=>new VideoRandomAccessStream(source,position);
    public IInputStream GetInputStreamAt(ulong value)=>new VideoRandomAccessStream(source,value);
    public IOutputStream GetOutputStreamAt(ulong value)=>throw new NotSupportedException();
    public IAsyncOperationWithProgress<IBuffer,uint> ReadAsync(IBuffer buffer,uint count,InputStreamOptions options)
        =>AsyncInfo.Run<IBuffer,uint>(async(ct,progress)=>{
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetimeToken);
            var size=checked((int)Math.Min(count,buffer.Capacity));
            if(options.HasFlag(InputStreamOptions.Partial))size=Math.Min(size,1024*1024);
            var bytes=new byte[size];
            var read=await source.ReadAtAsync(checked((long)position),bytes,linked.Token).ConfigureAwait(false);
            bytes.AsBuffer(0,read).CopyTo(buffer);buffer.Length=(uint)read;
            position+=(uint)read;progress.Report((uint)read);return buffer;
        });
    public IAsyncOperationWithProgress<uint,uint> WriteAsync(IBuffer buffer)=>throw new NotSupportedException();
    public IAsyncOperation<bool> FlushAsync()=>AsyncInfo.Run<bool>(_=>Task.FromResult(true));
    public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;lifetime.Cancel();lifetime.Dispose();}
}
