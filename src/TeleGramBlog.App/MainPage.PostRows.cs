using System.ComponentModel;
using TeleGramBlog.App.Views;
using Microsoft.UI.Xaml.Media;
namespace TeleGramBlog.App;
public partial class MainPage
{
    bool postRowsPending;
    void CardLayoutChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(HeightRequest))SchedulePostRows();}
    void SchedulePostRows()
    {
        if(postRowsPending)return;postRowsPending=true;
        Dispatcher.Dispatch(()=>{postRowsPending=false;UpdatePostRows();});
    }
    void UpdatePostRows()
    {
        if(!vm.IsPosts||PostGrid.Handler?.PlatformView is not Microsoft.UI.Xaml.DependencyObject root||
            FindList(root) is not {} list||list.ItemsPanelRoot is not Microsoft.UI.Xaml.Controls.ItemsWrapGrid panel)return;
        if(vm.Cards.Count==0){panel.ItemHeight=double.NaN;return;}
        foreach(var card in visibleCards)
        {
            if(card.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement native||native.ActualWidth<=0)continue;
            Microsoft.UI.Xaml.DependencyObject? ancestor=native;
            while(ancestor!=null&&ancestor is not Microsoft.UI.Xaml.Controls.GridViewItem)ancestor=VisualTreeHelper.GetParent(ancestor);
            if(ancestor is not Microsoft.UI.Xaml.Controls.GridViewItem container)continue;
            // WinUI's uniform panel otherwise measures only the first cell. Its slot must
            // fit albums even when the first item is a shorter photo/video/link card.
            var height=vm.Cards.Max(c=>MediaCardView.RequiredHeight(native.ActualWidth,c.Post.Media.Count));
            height=Math.Ceiling(height+container.Margin.Top+container.Margin.Bottom+container.Padding.Top+container.Padding.Bottom);
            if(double.IsNaN(panel.ItemHeight)||Math.Abs(panel.ItemHeight-height)>.5)panel.ItemHeight=height;
            break;
        }
    }
}
