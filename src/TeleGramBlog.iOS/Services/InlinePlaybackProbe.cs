#if DEBUG
using System.Text.Json;
using TeleGramBlog.Telegram;
using UIKit;
namespace Luma.Services;
internal static class InlinePlaybackProbe
{
    public static async Task RunAsync(UIView host)
    {
        var output = Path.Combine(FileSystem.CacheDirectory, "inline-playback-probe.json");
        var player = new NativeMedia();
        try
        {
            await using var input = await FileSystem.OpenAppPackageFileAsync("diagnostics/playback-audio.mp4");
            var source = new ChunkedMediaSource(input.Length, "video/mp4", async (offset, count, token) => {
                input.Position = offset;
                var bytes = new byte[(int)Math.Min(count, input.Length-offset)];
                await input.ReadExactlyAsync(bytes, token); return bytes;
            });
            var frame = new VideoFrame(16, 130, (double)host.Bounds.Width-32, 230, (double)host.Bounds.Width);
            player.PlayStream(source, host, frame);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            while(!(player.Position > 0.1)) await Task.Delay(100, timeout.Token);
            var embedded = player.IsEmbedded;
            var audioReady = player.AudioReady;
            var inlineFrame = player.PlayerFrame;
            var before = player.Position;
            player.Expand();
            await Task.Delay(600, timeout.Token);
            var expanded = player.IsExpanded && player.PlayerFrame?.Height > inlineFrame?.Height;
            var continued = player.Position > before;
            var seeked = await player.SeekAsync(3);
            player.Collapse();
            var collapsed = !player.IsExpanded && player.PlayerFrame == inlineFrame;
            await Task.Delay(300, timeout.Token);
            var position = player.Position;
            player.Close();
            var closed = !player.IsEmbedded;
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
                passed=audioReady&&embedded&&expanded&&continued&&seeked&&collapsed&&position>=3&&closed,
                audioReady, embedded, expanded, continued, seeked, collapsed, position, closed }));
        }
        catch(Exception e) { await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {passed=false,error=e.Message})); }
        finally { player.Close(); }
    }
}
#endif
