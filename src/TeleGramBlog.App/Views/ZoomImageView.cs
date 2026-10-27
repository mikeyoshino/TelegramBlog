namespace TeleGramBlog.App.Views;
// Viewer-only image surface. Native Windows scroll/zoom never changes shared Image styles.
public sealed class ZoomImageView:View
{
    public static readonly BindableProperty FilePathProperty=BindableProperty.Create(nameof(FilePath),typeof(string),typeof(ZoomImageView));
    public string? FilePath {get=>(string?)GetValue(FilePathProperty);set=>SetValue(FilePathProperty,value);}
    public event Action? Ready;
    public event Action? Failed;
    public event Action<float>? ZoomChanged;
    internal void ReportReady()=>Ready?.Invoke();
    internal void ReportFailed()=>Failed?.Invoke();
    internal void ReportZoom(float zoom)=>ZoomChanged?.Invoke(zoom);
    public void ZoomBy(float factor)=>(Handler as WinUI.ZoomImageHandler)?.ZoomBy(factor);
    public void Fit()=>(Handler as WinUI.ZoomImageHandler)?.Fit();
}
