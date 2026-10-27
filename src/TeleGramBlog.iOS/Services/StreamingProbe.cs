#if DEBUG
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using Foundation;
using System.Text.Json;
using TeleGramBlog.Telegram;
namespace Luma.Services;

// Opt-in simulator diagnostic, matching the desktop's isolated media smoke workflow.
// Uses bundled sample bytes and the production resource loader; never logs into Telegram.
internal static class StreamingProbe
{
    public static async Task RunAsync()
    {
        var output = Path.Combine(FileSystem.CacheDirectory, "streaming-probe.json");
        var offsets = new List<long>();
        try
        {
            await using var input = await FileSystem.OpenAppPackageFileAsync("wwwroot/assets/video.mp4");
            var length = input.Length;
            using var source = new ChunkedMediaSource(length, "video/mp4", async (offset, count, token) =>
            {
                offsets.Add(offset); input.Position = offset;
                var bytes = new byte[(int)Math.Min(count, length - offset)];
                await input.ReadExactlyAsync(bytes, token); return bytes;
            });
            using var loader = new StreamingResourceLoader(source);
            using var asset = new AVUrlAsset(new NSUrl("luma-media://preview/video"), new AVUrlAssetOptions());
            asset.ResourceLoader.SetDelegate(loader, DispatchQueue.MainQueue);
            using var item = new AVPlayerItem(asset);
            using var player = new AVPlayer(item);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            while(item.Status == AVPlayerItemStatus.Unknown) await Task.Delay(100, timeout.Token);
            if(item.Status != AVPlayerItemStatus.ReadyToPlay) throw new IOException(item.Error?.LocalizedDescription ?? "Player could not prepare the stream.");
            player.Play(); await Task.Delay(1200, timeout.Token); player.Pause();
            var advanced = player.CurrentTime.Seconds > 0;
            var seeked = await player.SeekAsync(CMTime.FromSeconds(3, 600));
            player.Play(); await Task.Delay(500, timeout.Token); player.Pause();
            var position = player.CurrentTime.Seconds;
            player.ReplaceCurrentItemWithPlayerItem(null);
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed = advanced && seeked && position >= 3,
                advanced, seeked, position, length, offsets, requests = offsets.Count }));
        }
        catch(Exception error)
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { passed = false, error = error.Message, offsets }));
        }
    }
}
#endif
