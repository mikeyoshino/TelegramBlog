using System.Text.Json;
using TeleGramBlog.App.Views;
namespace TeleGramBlog.App;
public partial class MainPage
{
    async Task CaptureCardsAsync()
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"card-verification");Directory.CreateDirectory(directory);
        try
        {
            await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);
            vm.Cards.Move(0,1); // Exercise a single-media first cell followed by an album.
            foreach(var size in new[]{(1280,720),(1920,1080)})
            {
                Window.X=0;Window.Y=0;Window.Width=size.Item1;Window.Height=size.Item2;await Task.Delay(1800);
                var list=PostGrid.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject root?FindList(root):null;
                var report=visibleCards.Select(card=>{
                    var native=card.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement;
                    Microsoft.UI.Xaml.DependencyObject? ancestor=native;
                    while(ancestor!=null&&ancestor is not Microsoft.UI.Xaml.Controls.GridViewItem)ancestor=Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(ancestor);
                    var container=ancestor as Microsoft.UI.Xaml.FrameworkElement;
                    var body=(card.Content as Border)?.Content as VerticalStackLayout;
                    return new{media=card.Card?.Post.Media.Count,card.Width,card.Height,card.HeightRequest,containerHeight=container?.ActualHeight,nativeHeight=native?.ActualHeight,bodyHeight=body?.Height,panel=(list?.ItemsPanelRoot as Microsoft.UI.Xaml.Controls.ItemsWrapGrid)?.ItemHeight};
                }).ToArray();
                await File.WriteAllTextAsync(Path.Combine(directory,$"geometry-{size.Item1}.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));
                await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,$"cards-{size.Item1}.png"),this);
                if(!report.Any(r=>r.media>1&&r.containerHeight!=null))throw new InvalidOperationException("Album fixture was not realized.");
                if(report.Any(r=>r.containerHeight is {} height&&r.nativeHeight is {} content&&height+1<content))throw new InvalidOperationException("Native grid cell clips the card.");
            }
            var recycled=visibleCards.First(c=>c.Card?.Post.Media.Count==1);
            var original=recycled.BindingContext;var width=((Microsoft.UI.Xaml.FrameworkElement)recycled.Handler!.PlatformView!).ActualWidth;
            recycled.SetCardWidth(width);var singleHeight=recycled.HeightRequest;
            recycled.BindingContext=vm.Cards.First(c=>c.Post.Media.Count>1);recycled.SetCardWidth(width);
            if(Math.Abs(recycled.HeightRequest-singleHeight-68)>1)throw new InvalidOperationException("Recycled single-to-album height did not expand.");
            recycled.BindingContext=original;recycled.SetCardWidth(width);
            if(Math.Abs(recycled.HeightRequest-singleHeight)>1)throw new InvalidOperationException("Recycled album-to-single height did not shrink.");
            await File.WriteAllTextAsync(Path.Combine(directory,"complete.txt"),"Card geometry captured.");
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(directory,"error.txt"),e.ToString());}
    }
}
