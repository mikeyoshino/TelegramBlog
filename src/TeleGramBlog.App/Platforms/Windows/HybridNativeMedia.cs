using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TeleGramBlog.App.WinUI;
using TeleGramBlog.Telegram;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Foundation;
namespace Luma.Services;

public sealed record VideoFrame(double X, double Y, double Width, double Height, double ViewportWidth);

// The same bounded range stream as the legacy desktop player; no full-file download.
public sealed class NativeMedia
{
    MediaPlayerElement? element;
    MediaPlayer? player;
    MediaSource? mediaSource;
    VideoRandomAccessStream? stream;
    ChunkedMediaSource? source;
    FrameworkElement? host;
    Panel? parent;
    Canvas? overlay;
    VideoFrame? frame;
    public void PlayStream(ChunkedMediaSource input, FrameworkElement web, VideoFrame bounds)
    {
        Close(); source=input; host=web;
        try
        {
            DependencyObject? ancestor=VisualTreeHelper.GetParent(web);
            while(ancestor!=null && ancestor is not Panel) ancestor=VisualTreeHelper.GetParent(ancestor);
            parent=ancestor as Panel ?? throw new InvalidOperationException("Video host is not ready.");
            overlay=new Canvas { HorizontalAlignment=Microsoft.UI.Xaml.HorizontalAlignment.Stretch,VerticalAlignment=Microsoft.UI.Xaml.VerticalAlignment.Stretch };
            if(parent is Microsoft.UI.Xaml.Controls.Grid grid)
            {
                Microsoft.UI.Xaml.Controls.Grid.SetRowSpan(overlay,Math.Max(1,grid.RowDefinitions.Count));
                Microsoft.UI.Xaml.Controls.Grid.SetColumnSpan(overlay,Math.Max(1,grid.ColumnDefinitions.Count));
            }
            parent.Children.Add(overlay);Canvas.SetZIndex(overlay,100);
            element=new MediaPlayerElement {AreTransportControlsEnabled=true,AutoPlay=false,Stretch=Microsoft.UI.Xaml.Media.Stretch.Uniform};
            element.DoubleTapped+=DoubleTapped;
            element.KeyDown+=KeyDown;
            overlay.Children.Add(element);
            player=new MediaPlayer {AutoPlay=false,Volume=1,IsMuted=false};
            player.MediaFailed+=Failed;
            element.SetMediaPlayer(player);
            stream=new(input);mediaSource=MediaSource.CreateFromStream(stream,input.ContentType);
            player.Source=mediaSource;
            parent.SizeChanged+=Resized;
            UpdateFrame(bounds);player.Play();
        }
        catch {Close();throw;}
    }
    public void UpdateFrame(VideoFrame bounds)
    {
        if(!double.IsFinite(bounds.X)||!double.IsFinite(bounds.Y)||!double.IsFinite(bounds.Width)||!double.IsFinite(bounds.Height)||!double.IsFinite(bounds.ViewportWidth)||bounds.Width<=0||bounds.Height<=0||bounds.ViewportWidth<=0)
            throw new ArgumentException("Invalid video frame.");
        frame=bounds;Layout();
    }
    void Layout()
    {
        if(element==null||host==null||overlay==null||frame==null)return;
        var scale=host.ActualWidth/frame.ViewportWidth;
        var origin=host.TransformToVisual(overlay).TransformPoint(new Windows.Foundation.Point(frame.X*scale,frame.Y*scale));
        Canvas.SetLeft(element,origin.X);Canvas.SetTop(element,origin.Y);
        element.Width=frame.Width*scale;element.Height=frame.Height*scale;
    }
    void Resized(object sender,SizeChangedEventArgs args)=>Layout();
    void DoubleTapped(object sender,DoubleTappedRoutedEventArgs args)
    {
        if(element==null)return;
        // Do not reinterpret gestures on transport buttons/sliders as fullscreen requests.
        var target=args.OriginalSource as DependencyObject;
        while(target!=null&&target!=element)
        {
            if(target is Microsoft.UI.Xaml.Controls.Primitives.ButtonBase or Microsoft.UI.Xaml.Controls.Primitives.RangeBase)return;
            target=VisualTreeHelper.GetParent(target);
        }
        element.IsFullWindow=!element.IsFullWindow;args.Handled=true;
    }
    void KeyDown(object sender,KeyRoutedEventArgs args)
    {
        if(element==null)return;
        if(args.Key==Windows.System.VirtualKey.Escape&&element.IsFullWindow){element.IsFullWindow=false;args.Handled=true;}
        else if(args.Key==Windows.System.VirtualKey.F){element.IsFullWindow=!element.IsFullWindow;args.Handled=true;}
    }
    void Failed(MediaPlayer sender,MediaPlayerFailedEventArgs args)
    {
        var current=element;
        current?.DispatcherQueue.TryEnqueue(()=>{
            if(sender!=player||overlay==null)return;
            var error=new Microsoft.UI.Xaml.Controls.TextBlock {Text="เล่นวิดีโอนี้ไม่ได้ กรุณาเปิดข้อความต้นทางใน Telegram",TextWrapping=TextWrapping.Wrap,Foreground=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),Width=Math.Max(100,current!.Width-32)};
            Canvas.SetLeft(error,Canvas.GetLeft(current)+16);Canvas.SetTop(error,Canvas.GetTop(current)+16);overlay.Children.Add(error);
        });
    }
    public void Expand(){if(element!=null)element.IsFullWindow=true;}
    public void Close()
    {
        if(parent!=null)parent.SizeChanged-=Resized;
        if(element!=null){element.IsFullWindow=false;element.DoubleTapped-=DoubleTapped;element.KeyDown-=KeyDown;element.SetMediaPlayer(null);}
        if(player!=null){player.MediaFailed-=Failed;player.Pause();player.Source=null;player.Dispose();}
        mediaSource?.Dispose();stream?.Dispose();source?.Dispose();
        if(overlay!=null)parent?.Children.Remove(overlay);
        element=null;player=null;mediaSource=null;stream=null;source=null;parent=null;overlay=null;host=null;frame=null;
    }
}
