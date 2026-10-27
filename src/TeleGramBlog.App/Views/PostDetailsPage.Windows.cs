using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
namespace TeleGramBlog.App.Views;
public sealed partial class PostDetailsPage
{
    Microsoft.UI.Xaml.FrameworkElement? keyRoot;
    AppWindowPresenter? previousPresenter;
    bool isFullscreen;
    AppWindow? NativeWindow=>(Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.AppWindow;
    void AttachKeyboard()
    {
        var native=Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
        if(native==keyRoot)return;DetachKeyboard();keyRoot=native;
        if(keyRoot==null)return;keyRoot.RequestedTheme=ElementTheme.Dark;
        keyRoot.AddHandler(UIElement.PreviewKeyDownEvent,new KeyEventHandler(ViewerKeyDown),true);Dispatcher.Dispatch(()=>back.Focus());
    }
    void DetachKeyboard(){keyRoot?.RemoveHandler(UIElement.PreviewKeyDownEvent,new KeyEventHandler(ViewerKeyDown));keyRoot=null;}
    async void ViewerKeyDown(object sender,KeyRoutedEventArgs e)
    {
        if(e.Handled)return;
        if(e.Key==VirtualKey.Escape)
        {
            e.Handled=true;
            if(video.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.MediaPlayerElement {IsFullWindow:true} player)player.IsFullWindow=false;
            else if(isFullscreen)ExitFullscreen();else await GoBack();return;
        }
        if(e.Key is not (VirtualKey.Left or VirtualKey.Right))return;
        for(var element=e.OriginalSource as DependencyObject;element!=null;element=VisualTreeHelper.GetParent(element))
            if(element is Microsoft.UI.Xaml.Controls.MediaPlayerElement or Microsoft.UI.Xaml.Controls.TextBox or Microsoft.UI.Xaml.Controls.Slider)return;
        e.Handled=true;await SelectMedia(session.Index+(e.Key==VirtualKey.Left?-1:1));
    }
    void ToggleFullscreen()
    {
        if(isFullscreen){ExitFullscreen();return;}if(NativeWindow is not {} window)return;
        previousPresenter=window.Presenter;window.SetPresenter(AppWindowPresenterKind.FullScreen);isFullscreen=true;
        fullscreen.Text="\uE73F";SemanticProperties.SetDescription(fullscreen,"ออกจากเต็มจอ");ToolTipProperties.SetText(fullscreen,"ออกจากเต็มจอ · Esc");
    }
    void ExitFullscreen()
    {
        if(!isFullscreen)return;
        if(NativeWindow is {} window){if(previousPresenter!=null)window.SetPresenter(previousPresenter);else window.SetPresenter(AppWindowPresenterKind.Overlapped);}
        isFullscreen=false;previousPresenter=null;fullscreen.Text="\uE740";SemanticProperties.SetDescription(fullscreen,"เต็มจอ");ToolTipProperties.SetText(fullscreen,"เต็มจอ");
    }
    void ShowMore(string original)
    {
        if(info.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement anchor)return;
        var menu=new Microsoft.UI.Xaml.Controls.MenuFlyout();var item=new Microsoft.UI.Xaml.Controls.MenuFlyoutItem{Text="เปิดโพสต์ต้นฉบับใน Telegram"};
        item.Click+=async(_,_)=>await Open(original);menu.Items.Add(item);menu.ShowAt(anchor);
    }
}
