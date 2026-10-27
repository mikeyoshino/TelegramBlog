using AVFoundation;
using Foundation;
using TeleGramBlog.Telegram;
namespace Luma.Services;

// iOS adapter for the same 256 KiB / 16 MiB bounded source used by the desktop decoder.
// Delegate callbacks and continuations run on the main queue; network reads are asynchronous.
public sealed class StreamingResourceLoader(ChunkedMediaSource source) : AVAssetResourceLoaderDelegate
{
    readonly Dictionary<AVAssetResourceLoadingRequest, CancellationTokenSource> pending = [];
    bool stopped;
    public override bool ShouldWaitForLoadingOfRequestedResource(AVAssetResourceLoader resourceLoader, AVAssetResourceLoadingRequest request)
    {
        if(stopped) return false;
        var cancellation = new CancellationTokenSource();
        pending.Add(request, cancellation);
        _ = SupplyAsync(request, cancellation);
        return true;
    }
    public override void DidCancelLoadingRequest(AVAssetResourceLoader resourceLoader, AVAssetResourceLoadingRequest request)
    {
        if(pending.TryGetValue(request, out var cancellation)) cancellation.Cancel();
    }
    async Task SupplyAsync(AVAssetResourceLoadingRequest request, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;
        try
        {
            if(request.ContentInformationRequest is {} info)
            {
                info.ContentLength = source.Length;
                info.ByteRangeAccessSupported = true;
                info.ContentType = source.ContentType switch
                {
                    "video/mp4" => "public.mpeg-4", "video/quicktime" => "com.apple.quicktime-movie",
                    "video/webm" => "org.webmproject.webm", _ => "public.movie"
                };
            }
            if(request.DataRequest is {} data)
            {
                var position = Math.Max(data.RequestedOffset, data.CurrentOffset);
                var end = data.RequestsAllDataToEndOfResource ? source.Length : Math.Min(source.Length, checked(data.RequestedOffset + data.RequestedLength));
                var buffer = new byte[ChunkedMediaSource.ChunkSize];
                while(position < end)
                {
                    var count = (int)Math.Min(buffer.Length, end - position);
                    var read = await source.ReadAtAsync(position, buffer.AsMemory(0, count), token);
                    token.ThrowIfCancellationRequested();
                    if(stopped || request.IsCancelled) return;
                    if(read == 0) throw new EndOfStreamException();
                    using var bytes = NSData.FromArray(buffer.AsSpan(0, read).ToArray());
                    data.Respond(bytes); position += read;
                }
            }
            token.ThrowIfCancellationRequested();
            if(!stopped && !request.IsCancelled) request.FinishLoading();
        }
        catch(OperationCanceledException) { }
        catch(Exception)
        {
            if(!stopped && !token.IsCancellationRequested && !request.IsCancelled)
                request.FinishLoadingWithError(new NSError(new NSString("Luma.Media"), -1,
                    new NSDictionary(NSError.LocalizedDescriptionKey, new NSString("ไม่สามารถโหลดวิดีโอได้ กรุณาลองอีกครั้งหรือเปิดใน Telegram"))));
        }
        finally { pending.Remove(request); cancellation.Dispose(); }
    }
    protected override void Dispose(bool disposing)
    {
        if(!stopped)
        {
            stopped = true;
            foreach(var cancellation in pending.Values) cancellation.Cancel();
            source.Dispose();
        }
        base.Dispose(disposing);
    }
}
