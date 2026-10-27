namespace TeleGramBlog.App.Views;
public sealed class VideoPlayerView:View
{
    public static readonly BindableProperty StreamSourceProperty=BindableProperty.Create(nameof(StreamSource),typeof(TeleGramBlog.Telegram.ChunkedMediaSource),typeof(VideoPlayerView));
    public TeleGramBlog.Telegram.ChunkedMediaSource? StreamSource {get=>(TeleGramBlog.Telegram.ChunkedMediaSource?)GetValue(StreamSourceProperty);set=>SetValue(StreamSourceProperty,value);}
    public static readonly BindableProperty FilePathProperty=BindableProperty.Create(nameof(FilePath),typeof(string),typeof(VideoPlayerView));
    public string? FilePath {get=>(string?)GetValue(FilePathProperty);set=>SetValue(FilePathProperty,value);}
    public event Action<string>? PlaybackFailed;
    public event Action? Ready;
    public event Action<bool>? BufferingChanged;
    internal void ReportBuffering(bool buffering)=>BufferingChanged?.Invoke(buffering);
    internal void ReportFailure(string message)=>PlaybackFailed?.Invoke(message);
    internal void ReportReady()=>Ready?.Invoke();
}
