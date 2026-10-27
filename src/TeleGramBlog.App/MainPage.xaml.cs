using TeleGramBlog.App.ViewModels;
using TeleGramBlog.App.Views;
using TeleGramBlog.Core;
namespace TeleGramBlog.App;
public partial class MainPage : ContentPage,IDisposable
{
    readonly MainViewModel vm=new(action=>MainThread.BeginInvokeOnMainThread(action));
    byte[]? displayedQr;
    public MainPage()
    {
        InitializeComponent();BindingContext=vm;
        vm.Cards.CollectionChanged+=(_,_)=>SchedulePostRows();PostGrid.Loaded+=(_,_)=>SchedulePostRows();
        vm.PropertyChanged+=(_,_)=>{
            UpdateSidebar();
            if(vm.IsPosts&&!vm.IsBusy)Dispatcher.Dispatch(()=>{
                PostGridSizeChanged(PostGrid,EventArgs.Empty);
                if(PostGrid.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement native){native.InvalidateMeasure();native.UpdateLayout();}
            });
            if(ReferenceEquals(displayedQr,vm.QrPng))return;
            displayedQr=vm.QrPng;
            var bytes=displayedQr;
            QrImage.Source=bytes==null?null:ImageSource.FromStream(()=>new MemoryStream(bytes,false));
        };
    }
    bool previewStarted;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if(Environment.GetCommandLineArgs().Contains("--design-preview")||Environment.GetCommandLineArgs().Contains("--capture-shell")||Environment.GetCommandLineArgs().Contains("--capture-viewer")||Environment.GetCommandLineArgs().Contains("--memory-probe")||Environment.GetCommandLineArgs().Contains("--capture-cards"))
        {if(!previewStarted){previewStarted=true;await vm.PreviewAsync();if(Environment.GetCommandLineArgs().Contains("--capture-shell"))_=CaptureShellAsync();if(Environment.GetCommandLineArgs().Contains("--capture-viewer"))_=CaptureViewerAsync();if(Environment.GetCommandLineArgs().Contains("--memory-probe"))_=MemoryProbeAsync();if(Environment.GetCommandLineArgs().Contains("--capture-cards"))_=CaptureCardsAsync();}}
        else await vm.InitializeAsync();
    }
    async void ConnectClicked(object? sender,EventArgs e)=>await vm.ConnectAsync();
    void SwitchLoginClicked(object? sender,EventArgs e)=>vm.SwitchLoginMode();
    async void PreviewClicked(object? sender,EventArgs e)=>await vm.PreviewAsync();
    void RestartClicked(object? sender,EventArgs e)=>vm.RestartLogin();
    async void GroupSelected(object? sender,SelectionChangedEventArgs e)
    {if(e.CurrentSelection.FirstOrDefault() is GroupInfo group){if(sender is CollectionView cv)cv.SelectedItem=null;await vm.SelectGroupAsync(group);}}
    async void TopicSelected(object? sender,SelectionChangedEventArgs e)
    {if(e.CurrentSelection.FirstOrDefault() is TopicInfo topic && vm.SelectedTopic?.Id!=topic.Id)await vm.SelectTopicAsync(topic);}
    void GroupsClicked(object? sender,EventArgs e){var offset=groupScroll;vm.BackToGroups();Dispatcher.Dispatch(()=>{if(vm.Groups.Count>offset)GroupGrid.ScrollTo(offset,position:ScrollToPosition.Start,animate:false);});}
    bool sidebarCollapsed;
    int groupScroll;
    readonly Dictionary<long,int> topicScroll=[];
    readonly Dictionary<(long,int),int> postScroll=[];
    void UpdateSidebar(){var visible=vm.HasTopicSidebar&&!sidebarCollapsed;TopicSidebar.IsVisible=visible;BrowserColumns.ColumnDefinitions[1].Width=visible?244:0;}
    void GroupGridSizeChanged(object? sender,EventArgs e){if(GroupGrid.Width>0)GroupLayout.Span=Math.Max(1,(int)(GroupGrid.Width/320));}
    void ShellListLoaded(object? sender,EventArgs e)
    {
        if(sender is CollectionView {Handler.PlatformView:Microsoft.UI.Xaml.DependencyObject root}&&FindList(root) is {} list)
        {
            var style=new Microsoft.UI.Xaml.Style {TargetType=list is Microsoft.UI.Xaml.Controls.GridView?typeof(Microsoft.UI.Xaml.Controls.GridViewItem):typeof(Microsoft.UI.Xaml.Controls.ListViewItem)};
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.Control.PaddingProperty,new Microsoft.UI.Xaml.Thickness(0)));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.Controls.Control.HorizontalContentAlignmentProperty,Microsoft.UI.Xaml.HorizontalAlignment.Stretch));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.FrameworkElement.MinWidthProperty,0d));
            style.Setters.Add(new Microsoft.UI.Xaml.Setter(Microsoft.UI.Xaml.FrameworkElement.MarginProperty,new Microsoft.UI.Xaml.Thickness(0)));
            list.ItemContainerStyle=style;
            list.Padding=new Microsoft.UI.Xaml.Thickness(0);
        }
    }
    static Microsoft.UI.Xaml.Controls.ListViewBase? FindList(Microsoft.UI.Xaml.DependencyObject root)
    {
        if(root is Microsoft.UI.Xaml.Controls.ListViewBase list)return list;
        for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            if(FindList(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i)) is {} child)return child;
        return null;
    }
    void GroupGridScrolled(object? sender,ItemsViewScrolledEventArgs e){if(vm.IsGroupPicker)groupScroll=Math.Max(0,e.FirstVisibleItemIndex);}
    void TopicListScrolled(object? sender,ItemsViewScrolledEventArgs e){if(!vm.IsBusy&&vm.SelectedGroup!=null)topicScroll[vm.SelectedGroup.Id]=Math.Max(0,e.FirstVisibleItemIndex);}
    void PostGridScrolled(object? sender,ItemsViewScrolledEventArgs e){if(!vm.IsBusy&&vm.SelectedGroup!=null&&vm.SelectedTopic!=null)postScroll[(vm.SelectedGroup.Id,vm.SelectedTopic.Id)]=Math.Max(0,e.FirstVisibleItemIndex);}
    async void GroupButtonClicked(object? sender,EventArgs e)
    {
        if(sender is Button {CommandParameter:GroupInfo group})
        {
            var offset=topicScroll.GetValueOrDefault(group.Id);
            await vm.SelectGroupAsync(group);UpdateSidebar();
            if(offset<vm.Topics.Count)TopicList.ScrollTo(offset,position:ScrollToPosition.Start,animate:false);
        }
    }
    async void TopicButtonClicked(object? sender,EventArgs e)
    {
        if(sender is Button {CommandParameter:TopicInfo topic})
        {
            var offset=vm.SelectedGroup==null?0:postScroll.GetValueOrDefault((vm.SelectedGroup.Id,topic.Id));
            await vm.SelectTopicAsync(topic);
            if(offset<vm.Cards.Count)PostGrid.ScrollTo(offset,position:ScrollToPosition.Start,animate:false);
        }
    }
    readonly Dictionary<long,int> overviewScroll=[];
    void OverviewListScrolled(object? sender,ItemsViewScrolledEventArgs e){if(vm.IsOverview&&!vm.IsBusy&&vm.SelectedGroup!=null)overviewScroll[vm.SelectedGroup.Id]=Math.Max(0,e.FirstVisibleItemIndex);}
    void OverviewClicked(object? sender,EventArgs e)
    {
        var offset=vm.SelectedGroup==null?0:overviewScroll.GetValueOrDefault(vm.SelectedGroup.Id);
        vm.BackToOverview();Dispatcher.Dispatch(()=>{if(offset<vm.Topics.Count)OverviewList.ScrollTo(offset,position:ScrollToPosition.Start,animate:false);});
    }
    void ClearGroupSearchClicked(object? sender,EventArgs e)=>vm.GroupQuery="";
    void ClearPostSearchClicked(object? sender,EventArgs e)=>vm.PostQuery="";
    async void RetryClicked(object? sender,EventArgs e)=>await vm.RetryAsync();
    void AccountClicked(object? sender,EventArgs e)
    {
        if(sender is Button {Handler.PlatformView:Microsoft.UI.Xaml.FrameworkElement anchor})
        {
            var menu=new Microsoft.UI.Xaml.Controls.MenuFlyout();
            menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutItem {Text=vm.AccountName,IsEnabled=false});
            menu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());
            var logout=new Microsoft.UI.Xaml.Controls.MenuFlyoutItem {Text="ออกจากระบบ Telegram",IsEnabled=vm.IsReady};
            logout.Click+=async(_,_)=>await vm.LogoutAsync();menu.Items.Add(logout);menu.ShowAt(anchor);
        }
    }
    async Task CaptureShellAsync()
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"screenshots");Directory.CreateDirectory(directory);
        try
        {
            File.Delete(Path.Combine(directory,"complete.txt"));File.Delete(Path.Combine(directory,"capture-error.txt"));
            await File.WriteAllTextAsync(Path.Combine(directory,"capture-report.txt"),"");
            foreach(var size in new[]{(1280,720),(1440,900),(1920,1080)})
            {
                Window.X=0;Window.Y=0;Window.Width=size.Item1;Window.Height=size.Item2;vm.BackToGroups();await Task.Delay(1600);
                await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,$"groups-{size.Item1}.png"),this);
                await vm.SelectGroupAsync(vm.Groups[0]);await Task.Delay(1000);
                await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,$"overview-{size.Item1}.png"),this);
                await vm.SelectTopicAsync(vm.Topics[1]);await Task.Delay(2000);
                await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,$"posts-{size.Item1}.png"),this);
            }
            Window.X=0;Window.Y=0;Window.Width=1280;Window.Height=720;vm.PostQuery="__no_matching_post__";await Task.Delay(600);
            await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"search-empty.png"),this);vm.PostQuery="";
            vm.SetVerificationGroups(Enumerable.Range(1,80).Select(i=>new GroupInfo(1000+i,$"ทดสอบชื่อกลุ่มยาว {i:00} — กลุ่มสำหรับตรวจข้อความภาษาไทยและการเลื่อนรายการ",true)));
            await Task.Delay(800);await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"stress-groups-top.png"),this);
            GroupGrid.ScrollTo(79,position:ScrollToPosition.End,animate:false);await ScrollToEndForCapture(GroupGrid);
            await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"stress-groups-last.png"),this);
            await vm.SelectGroupAsync(vm.Groups[0]);vm.SetVerificationTopics(Enumerable.Range(1,80).Select(i=>new TopicInfo(i,$"ทดสอบหัวข้อยาว {i:00} — อ่านภาษาไทยและเลือกด้วยแป้นพิมพ์")));
            await Task.Delay(600);await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"stress-topics-top.png"),this);
            TopicList.ScrollTo(79,position:ScrollToPosition.End,animate:false);OverviewList.ScrollTo(79,position:ScrollToPosition.End,animate:false);await ScrollToEndForCapture(TopicList);await ScrollToEndForCapture(OverviewList);
            await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"stress-topics-last.png"),this);
            vm.SetVerificationTopics([]);await Task.Delay(500);await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"topics-empty.png"),this);
            vm.SetVerificationGroups([]);await Task.Delay(500);await WinUI.ShellCapture.SaveAsync(Window,Path.Combine(directory,"groups-empty.png"),this);
            await File.WriteAllTextAsync(Path.Combine(directory,"complete.txt"),"Native screenshots completed.");
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(directory,"capture-error.txt"),e.ToString());}
    }
    async Task CaptureViewerAsync()
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"viewer-screenshots");Directory.CreateDirectory(directory);
        try
        {
            await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);
            var card=vm.Cards.First(c=>c.Post.Media.Count>1);
            var viewer=new PostDetailsPage(card,vm.Service,vm.SelectedGroup,true,topicTitle:vm.SelectedTopic?.Title);
            await Navigation.PushAsync(viewer);await viewer.CaptureVerificationAsync(directory);
        }
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(directory,"error.txt"),e.ToString());}
    }
    async Task MemoryProbeAsync()
    {
        var directory=Path.Combine(AppContext.BaseDirectory,"memory-probe");Directory.CreateDirectory(directory);
        try{await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);await PostDetailsPage.RunMemoryProbeAsync(Navigation,vm.Cards.First(c=>c.Post.Media.Count>1),vm.SelectedGroup,directory,()=>visibleCards.Count);}
        catch(Exception e){await File.WriteAllTextAsync(Path.Combine(directory,"error.txt"),e.ToString());}
    }
    static Microsoft.UI.Xaml.Controls.ScrollViewer? FindScroller(Microsoft.UI.Xaml.DependencyObject root)
    {
        if(root is Microsoft.UI.Xaml.Controls.ScrollViewer scroll)return scroll;
        for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            if(FindScroller(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i)) is {} child)return child;
        return null;
    }
    static async Task ScrollToEndForCapture(CollectionView view)
    {
        for(var i=0;i<3;i++)
        {
            await Task.Delay(350);
            if(view.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject root&&FindScroller(root) is {} scroll)
                scroll.ChangeView(null,scroll.ScrollableHeight,null,true);
        }
        await Task.Delay(400);
    }
    async void RefreshClicked(object? sender,EventArgs e)=>await vm.RefreshAsync();
    async void LogoutClicked(object? sender,EventArgs e)=>await vm.LogoutAsync();
    void CancelClicked(object? sender,EventArgs e)=>vm.Cancel();
    async void PreviousClicked(object? sender,EventArgs e)=>await vm.PreviousAsync();
    async void NextClicked(object? sender,EventArgs e)=>await vm.NextAsync();
    async void NumberClicked(object? sender,EventArgs e){if(sender is Button {CommandParameter:int n})await vm.ChangePageAsync(n);}
    async void PageSizeChanged(object? sender,EventArgs e){if(vm.IsWorkspace && !vm.IsBusy)await vm.ChangePageAsync(1);}
    void ToggleTopicsClicked(object? sender,EventArgs e)
    {sidebarCollapsed=!sidebarCollapsed;UpdateSidebar();}
    readonly HashSet<MediaCardView> visibleCards=[];
    void PostGridSizeChanged(object? sender,EventArgs e)
    {
        if(PostGrid.Width<=0)return;
        PostLayout.Span=PostGrid.Width>=1400?4:PostGrid.Width>=1000?3:PostGrid.Width>=620?2:1;
        foreach(var card in visibleCards)card.SetCardWidth((PostGrid.Width-(PostLayout.Span-1)*16)/PostLayout.Span);
        SchedulePostRows();
    }
    void MediaCardLoaded(object? sender,EventArgs e)
    {
        if(sender is MediaCardView view)
        {
            visibleCards.Add(view);view.SetCardWidth((PostGrid.Width-(PostLayout.Span-1)*16)/PostLayout.Span);
            view.PropertyChanged-=CardLayoutChanged;view.PropertyChanged+=CardLayoutChanged;SchedulePostRows();
            view.LoadThumbnail=(media,ct)=>vm.IsDemo?Task.FromResult<string?>($"preview{(media.MessageId%6)+1}.jpg"):vm.Service?.GetMediaAsync(media,false,ct)??Task.FromResult<string?>(null);
        }
    }
    void MediaCardUnloaded(object? sender,EventArgs e){if(sender is MediaCardView view){view.PropertyChanged-=CardLayoutChanged;visibleCards.Remove(view);}}
    async void MediaCardOpened(object? sender,EventArgs e)
    {if(sender is MediaCardView {Card:{} card} view)await Navigation.PushAsync(new PostDetailsPage(card,vm.Service,vm.SelectedGroup,vm.IsDemo,view.SelectedIndex,vm.SelectedTopic?.Title,()=>RestoreCardFocus(view)));}
    void RestoreCardFocus(MediaCardView view)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250),()=>{
            if(view.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject root&&FindCardButton(root) is {} button)button.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
        });
    }
    static Microsoft.UI.Xaml.Controls.Button? FindCardButton(Microsoft.UI.Xaml.DependencyObject root)
    {
        if(root is Microsoft.UI.Xaml.Controls.Button button)return button;
        for(var i=0;i<Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);i++)
            if(FindCardButton(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root,i)) is {} child)return child;
        return null;
    }
    async void MediaCardLinkClicked(object? sender,EventArgs e)
    {if(sender is MediaCardView {Card:{} card}&&card.Post.Links.FirstOrDefault() is {} link)await OpenUrl(link);}
    async void CardClicked(object? sender,EventArgs e){if(sender is Button {CommandParameter:CardViewModel c})await Details(c);}
    async void CardTapped(object? sender,TappedEventArgs e){if(e.Parameter is CardViewModel c)await Details(c);}
    async Task Details(CardViewModel card)=>await Navigation.PushAsync(new PostDetailsPage(card,vm.Service,vm.SelectedGroup,vm.IsDemo,topicTitle:vm.SelectedTopic?.Title));
    async void LinkClicked(object? sender,EventArgs e){if(sender is Button {CommandParameter:CardViewModel c} && c.Post.Links.FirstOrDefault() is {} link)await OpenUrl(link);}
    async Task OpenUrl(string url)
    {if(!LinkExtractor.IsSafe(url))return;try{await Launcher.Default.OpenAsync(url);}catch{await DisplayAlertAsync("Could not open link","No application was available to open this link.","OK");}}
    public void Dispose()=>vm.Dispose();
}
