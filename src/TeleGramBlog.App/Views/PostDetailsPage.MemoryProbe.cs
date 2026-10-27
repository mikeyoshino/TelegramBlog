using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TeleGramBlog.App.ViewModels;
using TeleGramBlog.Core;
namespace TeleGramBlog.App.Views;

// Explicit offline diagnostic only. Forced GC is measurement, never an application policy.
public sealed partial class PostDetailsPage
{
    internal static async Task RunMemoryProbeAsync(INavigation navigation,CardViewModel card,GroupInfo? group,string directory,Func<int> cardCount)
    {
        if(!Environment.GetCommandLineArgs().Contains("--memory-probe"))throw new InvalidOperationException();
        Directory.CreateDirectory(directory);var file=Path.Combine(directory,"samples.jsonl");await File.WriteAllTextAsync(file,"");
        File.Delete(Path.Combine(directory,"complete.txt"));File.Delete(Path.Combine(directory,"error.txt"));
        var pages=new List<WeakReference>();var resources=new List<(string Kind,WeakReference Reference)>();
        var cycles=Environment.GetCommandLineArgs().Select(a=>a.StartsWith("--memory-cycles=")&&int.TryParse(a[16..],out var n)?n:0).FirstOrDefault(n=>n>0);
        if(cycles==0)cycles=40;cycles=Math.Clamp(cycles,5,500);
        async Task Sample(int cycle,string phase="cycling")
        {
            var allocated=GC.GetTotalAllocatedBytes();var before=GC.GetTotalMemory(false);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(300);
            using var process=Process.GetCurrentProcess();process.Refresh();
            var sample=new{cycle,phase,registeredCards=cardCount(),managedBeforeMiB=before/1048576d,managedAfterMiB=GC.GetTotalMemory(false)/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,workingSetMiB=process.WorkingSet64/1048576d,allocatedMiB=allocated/1048576d,liveViewers=pages.Count(w=>w.IsAlive),liveResources=resources.GroupBy(r=>r.Kind).ToDictionary(g=>g.Key,g=>g.Count(r=>r.Reference.IsAlive)),handles=process.HandleCount,gen0=GC.CollectionCount(0),gen1=GC.CollectionCount(1),gen2=GC.CollectionCount(2)};
            await File.AppendAllTextAsync(file,JsonSerializer.Serialize(sample)+"\n");
        }
        await Sample(0);
        for(var i=1;i<=cycles;i++){pages.Add(await MemoryCycle(navigation,card,group,resources));if(i%5==0)await Sample(i);}
        await Task.Delay(10000);await Sample(cycles,"idle10s");await Task.Delay(1000);await Sample(cycles,"drained");
        await File.WriteAllTextAsync(Path.Combine(directory,"complete.txt"),$"{cycles} cycles; photoOnly={Environment.GetCommandLineArgs().Contains("--memory-photo-only")}; forced GC every 5 cycles plus idle/drained samples.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static async Task<WeakReference> MemoryCycle(INavigation navigation,CardViewModel card,GroupInfo? group,List<(string Kind,WeakReference Reference)> resources)
    {
        var page=new PostDetailsPage(card,null,group,true,topicTitle:"Memory diagnostic — sample media");
        await navigation.PushAsync(page,false);
        async Task Ready(){for(var i=0;i<100&&page.session.State==ViewerLoadState.Loading;i++)await Task.Delay(50);if(page.session.State!=ViewerLoadState.Ready)throw new InvalidOperationException("Media did not load in memory probe.");}
        await Ready();
        if(!Environment.GetCommandLineArgs().Contains("--memory-photo-only"))
        {
        await page.SelectMedia(1);await Ready();
        var native=(Microsoft.UI.Xaml.Controls.MediaPlayerElement)page.video.Handler!.PlatformView!;
        native.MediaPlayer.Play();await Task.Delay(150);native.MediaPlayer.Pause();
        resources.Add(("VideoSource",new(page.videoSource)));resources.Add(("NativePlayer",new(native.MediaPlayer)));
        }
        await page.SelectMedia(2);await Ready();
        foreach(var (kind,target) in new (string,object?)[]{("ImageView",page.image),("ImageHandler",page.image.Handler),("ImageNative",page.image.Handler?.PlatformView),("VideoHandler",page.video.Handler),("VideoNative",page.video.Handler?.PlatformView),("BackButton",page.back),("BackHandler",page.back.Handler),("Root",page.root),("Session",page.session)})resources.Add((kind,new(target)));
        var weak=new WeakReference(page);await navigation.PopAsync(false);await Task.Delay(80);return weak;
    }
}
