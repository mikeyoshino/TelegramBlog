namespace TeleGramBlog.App.Views;

// Runs only through the explicit --capture-viewer diagnostic entry point, never against an account.
public sealed partial class PostDetailsPage
{
    internal async Task CaptureVerificationAsync(string directory)
    {
        if(!demo||!Environment.GetCommandLineArgs().Contains("--capture-viewer"))throw new InvalidOperationException();
        var report=new List<string>();
        var captureWindow=Window;
        var navigation=captureWindow.Page!.Navigation;
        File.Delete(Path.Combine(directory,"error.txt"));File.Delete(Path.Combine(directory,"complete.txt"));
        await File.WriteAllTextAsync(Path.Combine(directory,"capture-report.txt"),"");
        async Task WaitReady()
        {
            for(var i=0;i<60&&session.State==Core.ViewerLoadState.Loading;i++)await Task.Delay(100);
            if(session.State!=Core.ViewerLoadState.Ready)throw new InvalidOperationException($"Media did not become ready: {session.State}");
        }
        void Check(bool valid,string name){if(!valid)throw new InvalidOperationException(name);report.Add("PASS: "+name);}
        async Task Save(string name){await Task.Delay(500);await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,name+".png"),this);}
        foreach(var size in new[]{(1280,720),(1920,1080)})
        {
            Window.X=0;Window.Y=0;Window.Width=size.Item1;Window.Height=size.Item2;await Task.Delay(1000);await WaitReady();
            Check(stage.Height>400&&filmstrip.Height==96,"Stage expands and 96 DIP filmstrip remains visible at "+size.Item1);
            Check(!details.IsVisible,"Details closed by default at "+size.Item1);
            Check(keyRoot!=null,"Native keyboard listener attached at "+size.Item1);
            await Save("viewer-"+size.Item1);ToggleDetails();await Save("details-"+size.Item1);ToggleDetails();
        }
        image.ZoomBy(2);await Task.Delay(300);Check(zoomLabel.Text=="2×","Zoom reaches 2x");await Save("zoomed");image.Fit();await Task.Delay(250);Check(zoomLabel.Text=="พอดี","Fit resets zoom");
        await SelectMedia(1);await WaitReady();Check(!zoomIn.IsVisible,"Image tools hidden for video");await Save("video");
        await SelectMedia(2);await WaitReady();Check(!next.IsEnabled,"No wrapping at final media");
        var portrait=Path.Combine(AppContext.BaseDirectory,"preview5.scale-100.jpg");
        image.FilePath=portrait;await Task.Delay(500);await Save("portrait");
        var square=Directory.GetFiles(AppContext.BaseDirectory,"appicon*.png").FirstOrDefault();
        if(square!=null){image.FilePath=square;await Task.Delay(500);await Save("square-fixture");}
        Window.Width=880;Window.Height=720;ToggleDetails();await Save("details-narrow");Check(body.ColumnDefinitions[1].Width.Value==0,"Narrow details overlay preserves stage width");ToggleDetails();
        image.FilePath=null;loadToken=session.Begin();ShowState();await Save("loading-fixture");
        image.FilePath=Path.Combine(directory,"missing-image.jpg");await Task.Delay(700);Check(retry.IsVisible&&session.State==Core.ViewerLoadState.Error,"Decode failure shows retry");await Save("error-fixture");
        await LoadMedia();await WaitReady();Check(!retry.IsVisible,"Successful retry removes error controls");
        var first=SelectMedia(0);var second=SelectMedia(1);var third=SelectMedia(2);await Task.WhenAll(first,second,third);await WaitReady();
        Check(session.Index==2&&videoSource==null&&image.IsVisible,"Rapid photo/video/photo selection ends on latest photo");
        ToggleFullscreen();await Task.Delay(500);Check(isFullscreen,"Full screen enters");ExitFullscreen();await Task.Delay(500);Check(!isFullscreen,"Full screen exits");
        await GoBack();await Task.Delay(500);Check(videoSource==null&&!active,"Exit releases video");
        var single=new PostDetailsPage(new ViewModels.CardViewModel(card.Post with{Media=[card.Post.Media[0]]}),null,group,true,topicTitle:"ตรวจสื่อรายการเดียว");
        await navigation.PushAsync(single);await Task.Delay(1000);Check(single.active&&single.Handler!=null,"Singleton mounted in native navigation");
        Check(!single.filmstrip.IsVisible&&!single.previous.IsVisible&&!single.next.IsVisible,"Singleton hides filmstrip and arrows");
        await WinUI.ShellCapture.SaveAsync(captureWindow,Path.Combine(directory,"single.png"),single);await single.GoBack();
        var many=new PostDetailsPage(new ViewModels.CardViewModel(card.Post with{Media=Enumerable.Range(0,24).Select(i=>card.Post.Media[i%3]).ToArray()}),null,group,true,topicTitle:"ตรวจแถบภาพย่อยาว · สื่อตัวอย่างซ้ำ");
        await navigation.PushAsync(many);await Task.Delay(1000);Check(many.active&&many.Handler!=null,"Long strip mounted in native navigation");await many.SelectMedia(23);await Task.Delay(800);
        Check(many.gallery.ScrollX>0&&many.stripPrevious.IsEnabled&&!many.next.IsEnabled,"Long strip scrolls selected last thumbnail into view");
        await WinUI.ShellCapture.SaveAsync(captureWindow,Path.Combine(directory,"long-filmstrip-fixture.png"),many);await many.GoBack();
        var empty=new PostDetailsPage(new ViewModels.CardViewModel(card.Post with{Media=[]}),null,group,true,topicTitle:"ตรวจโพสต์ไม่มีไฟล์สื่อ");
        await navigation.PushAsync(empty);await Task.Delay(500);Check(empty.active&&!empty.progress.IsVisible&&!empty.cancel.IsVisible&&!empty.filmstrip.IsVisible,"No-media post has no perpetual loading or navigation");await empty.GoBack();
        await File.WriteAllLinesAsync(Path.Combine(directory,"checks.txt"),report);await File.WriteAllTextAsync(Path.Combine(directory,"complete.txt"),"Native viewer verification completed.");
    }
}
