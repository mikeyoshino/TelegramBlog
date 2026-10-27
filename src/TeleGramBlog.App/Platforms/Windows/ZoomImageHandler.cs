using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using TeleGramBlog.App.Views;
using Windows.Storage;
namespace TeleGramBlog.App.WinUI;

public sealed class ZoomImageHandler:ViewHandler<ZoomImageView,ScrollViewer>
{
    public static readonly IPropertyMapper<ZoomImageView,ZoomImageHandler> Mapper=new PropertyMapper<ZoomImageView,ZoomImageHandler>(ViewMapper)
    {[nameof(ZoomImageView.FilePath)]=(h,v)=>h.Load(v.FilePath)};
    readonly Microsoft.UI.Xaml.Controls.Image image=new(){Stretch=Microsoft.UI.Xaml.Media.Stretch.Uniform,HorizontalAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Stretch,VerticalAlignment=Microsoft.UI.Xaml.VerticalAlignment.Stretch};
    readonly Microsoft.UI.Xaml.Controls.Grid canvas=new();
    int generation;
    Windows.Foundation.Point? drag;
    double originX,originY;
    public ZoomImageHandler():base(Mapper){}
    protected override ScrollViewer CreatePlatformView()
    {
        canvas.Children.Add(image);
        var scroll=new ScrollViewer{Content=canvas,ZoomMode=ZoomMode.Enabled,MinZoomFactor=1,MaxZoomFactor=8,
            HorizontalScrollMode=Microsoft.UI.Xaml.Controls.ScrollMode.Enabled,VerticalScrollMode=Microsoft.UI.Xaml.Controls.ScrollMode.Enabled,
            HorizontalScrollBarVisibility=Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Hidden,
            HorizontalContentAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Center,VerticalContentAlignment=Microsoft.UI.Xaml.VerticalAlignment.Center,
            IsTabStop=false};
        scroll.SizeChanged+=SizeChanged;scroll.ViewChanged+=ViewChanged;
        scroll.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler(PointerPressed),true);
        scroll.AddHandler(UIElement.PointerMovedEvent,new PointerEventHandler(PointerMoved),true);
        scroll.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler(PointerReleased),true);
        scroll.PointerCaptureLost+=PointerCaptureLost;
        return scroll;
    }
    void SizeChanged(object sender,SizeChangedEventArgs e){var scroll=(ScrollViewer)sender;canvas.Width=Math.Max(1,scroll.ActualWidth);canvas.Height=Math.Max(1,scroll.ActualHeight);}
    void ViewChanged(object? sender,ScrollViewerViewChangedEventArgs e)=>VirtualView?.ReportZoom(((ScrollViewer)sender!).ZoomFactor);
    void PointerPressed(object sender,PointerRoutedEventArgs e)
    {
        var scroll=(ScrollViewer)sender;var point=e.GetCurrentPoint(scroll);
        if(scroll.ZoomFactor<=1||!point.Properties.IsLeftButtonPressed)return;
        drag=point.Position;originX=scroll.HorizontalOffset;originY=scroll.VerticalOffset;scroll.CapturePointer(e.Pointer);e.Handled=true;
    }
    void PointerMoved(object sender,PointerRoutedEventArgs e)
    {
        if(drag is not {} start)return;var scroll=(ScrollViewer)sender;var point=e.GetCurrentPoint(scroll).Position;
        scroll.ChangeView(originX+start.X-point.X,originY+start.Y-point.Y,null,true);e.Handled=true;
    }
    void PointerReleased(object sender,PointerRoutedEventArgs e){drag=null;((ScrollViewer)sender).ReleasePointerCapture(e.Pointer);}
    void PointerCaptureLost(object sender,PointerRoutedEventArgs e)=>drag=null;
    async void Load(string? path)
    {
        var request=++generation;image.Source=null;Fit();if(string.IsNullOrEmpty(path))return;
        try
        {
            if(!Path.IsPathRooted(path))
            {
                var direct=Path.Combine(AppContext.BaseDirectory,path);
                path=File.Exists(direct)?direct:Path.Combine(AppContext.BaseDirectory,Path.GetFileNameWithoutExtension(path)+".scale-100"+Path.GetExtension(path));
            }
            var file=await StorageFile.GetFileFromPathAsync(path);
            using var stream=await file.OpenReadAsync();var bitmap=new BitmapImage();await bitmap.SetSourceAsync(stream);
            if(request!=generation)return;image.Source=bitmap;VirtualView?.ReportReady();
        }
        catch{if(request==generation)VirtualView?.ReportFailed();}
    }
    public void Fit(){drag=null;PlatformView?.ChangeView(0,0,1,true);VirtualView?.ReportZoom(1);}
    public void ZoomBy(float factor)
    {
        var scroll=PlatformView;if(scroll==null)return;
        var next=Math.Clamp(scroll.ZoomFactor*factor,1,8);var ratio=next/scroll.ZoomFactor;
        scroll.ChangeView((scroll.HorizontalOffset+scroll.ViewportWidth/2)*ratio-scroll.ViewportWidth/2,
            (scroll.VerticalOffset+scroll.ViewportHeight/2)*ratio-scroll.ViewportHeight/2,next,true);
    }
    protected override void DisconnectHandler(ScrollViewer platformView)
    {
        generation++;drag=null;platformView.SizeChanged-=SizeChanged;platformView.ViewChanged-=ViewChanged;
        platformView.RemoveHandler(UIElement.PointerPressedEvent,new PointerEventHandler(PointerPressed));
        platformView.RemoveHandler(UIElement.PointerMovedEvent,new PointerEventHandler(PointerMoved));
        platformView.RemoveHandler(UIElement.PointerReleasedEvent,new PointerEventHandler(PointerReleased));
        platformView.PointerCaptureLost-=PointerCaptureLost;image.Source=null;canvas.Children.Clear();platformView.Content=null;
        base.DisconnectHandler(platformView);
    }
}
