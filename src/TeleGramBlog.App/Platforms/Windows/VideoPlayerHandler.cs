using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Controls;
using TeleGramBlog.App.Views;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
namespace TeleGramBlog.App.WinUI;
public sealed class VideoPlayerHandler:ViewHandler<VideoPlayerView,MediaPlayerElement>
{
    public static readonly IPropertyMapper<VideoPlayerView,VideoPlayerHandler> VideoMapper=new PropertyMapper<VideoPlayerView,VideoPlayerHandler>(ViewMapper)
    {
        [nameof(VideoPlayerView.FilePath)]=(handler,view)=>handler.ChangeSource(view.FilePath),
        [nameof(VideoPlayerView.StreamSource)]=(handler,view)=>handler.ChangeSource(view.FilePath)
    };
    MediaPlayer? player;
    MediaSource? source;
    VideoRandomAccessStream? randomStream;
    int generation;
    public VideoPlayerHandler():base(VideoMapper){}
    protected override MediaPlayerElement CreatePlatformView()
    {
        var element=new MediaPlayerElement {AreTransportControlsEnabled=true,AutoPlay=false,Stretch=Microsoft.UI.Xaml.Media.Stretch.Uniform};
        return element;
    }
    void Opened(MediaPlayer sender,object args)
    {
        var request=generation;
        PlatformView?.DispatcherQueue.TryEnqueue(()=>{if(request==generation&&sender==player&&source!=null)VirtualView?.ReportReady();});
    }
    void Failed(MediaPlayer sender,MediaPlayerFailedEventArgs args)
    {
        var request=generation;
        PlatformView?.DispatcherQueue.TryEnqueue(()=>{if(request==generation&&sender==player&&source!=null)VirtualView?.ReportFailure("Windows could not play this video format. Open the original in Telegram.");});
    }
    void ReleasePlayer()
    {
        if(player!=null){player.MediaOpened-=Opened;player.MediaFailed-=Failed;player.PlaybackSession.BufferingStarted-=BufferingStarted;player.PlaybackSession.BufferingEnded-=BufferingEnded;player.Pause();player.Source=null;PlatformView.SetMediaPlayer(null);player.Dispose();player=null;}
        source?.Dispose();source=null;randomStream?.Dispose();randomStream=null;
    }
    async void ChangeSource(string? path)
    {
        var request=++generation;
        ReleasePlayer();PlatformView.IsFullWindow=false;
        var streaming=VirtualView.StreamSource;
        if(streaming==null&&string.IsNullOrWhiteSpace(path))return;
        try
        {
            var file=streaming==null?await StorageFile.GetFileFromPathAsync(path!):null;
            if(request!=generation)return;
            player=new MediaPlayer {AutoPlay=false};player.MediaOpened+=Opened;player.MediaFailed+=Failed;
            player.PlaybackSession.BufferingStarted+=BufferingStarted;player.PlaybackSession.BufferingEnded+=BufferingEnded;
            PlatformView.SetMediaPlayer(player);
            if(streaming!=null){randomStream=new(streaming);source=MediaSource.CreateFromStream(randomStream,streaming.ContentType);}
            else source=MediaSource.CreateFromStorageFile(file!);
            player.Source=source;
        }
        catch(Exception){if(request==generation)VirtualView?.ReportFailure("Unable to open this video stream. Try again or open Telegram.");}
    }
    protected override void DisconnectHandler(MediaPlayerElement platformView)
    {
        generation++;
        ReleasePlayer();base.DisconnectHandler(platformView);
    }
    void BufferingStarted(MediaPlaybackSession sender,object args)=>Buffering(sender,true);
    void BufferingEnded(MediaPlaybackSession sender,object args)=>Buffering(sender,false);
    void Buffering(MediaPlaybackSession sender,bool buffering)
    {
        var request=generation;PlatformView?.DispatcherQueue.TryEnqueue(()=>{if(request==generation&&player?.PlaybackSession==sender)VirtualView?.ReportBuffering(buffering);});
    }
}
