using TeleGramBlog.App.ViewModels;
using TeleGramBlog.Core;
using TeleGramBlog.Telegram;
namespace TeleGramBlog.App.Views;

public sealed partial class PostDetailsPage:ContentPage
{
    static readonly Color Surface=Color.FromArgb("#191D24"),Muted=Color.FromArgb("#AEB8C8"),Ink=Color.FromArgb("#EDF2FA"),Accent=Color.FromArgb("#4297FF");
    readonly CardViewModel card;
    readonly TelegramService? service;
    readonly GroupInfo? group;
    readonly bool demo;
    readonly ViewerSession session;
    readonly ZoomImageView image=new();
    readonly VideoPlayerView video=new(){IsVisible=false};
    readonly Grid root=new(){RowDefinitions={new(56),new(GridLength.Star)},BackgroundColor=Color.FromArgb("#101216")};
    readonly Grid body=new(){ColumnDefinitions={new(GridLength.Star),new(0)}};
    readonly Grid mediaColumn=new(){RowDefinitions={new(GridLength.Star),new(96)}};
    readonly Grid stage=new(){BackgroundColor=Color.FromArgb("#101216")};
    readonly Grid details=new(){IsVisible=false,BackgroundColor=Surface,RowDefinitions={new(56),new(GridLength.Star)},Padding=new Thickness(20,0)};
    readonly Label counter=Text("",14),zoomLabel=Text("พอดี",12),fileInfo=Text("",13);
    readonly Label state=Text("",14);
    readonly ActivityIndicator progress=new(){Color=Accent,WidthRequest=28,HeightRequest=28};
    readonly VerticalStackLayout status=new(){Spacing=12,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center,MaximumWidthRequest=360};
    readonly ScrollView gallery=new(){Orientation=ScrollOrientation.Horizontal,HorizontalScrollBarVisibility=ScrollBarVisibility.Never};
    readonly HorizontalStackLayout strip=new(){Spacing=8,Padding=new Thickness(0,12)};
    readonly Grid filmstrip=new(){BackgroundColor=Surface,ColumnDefinitions={new(44),new(GridLength.Star),new(44)},ColumnSpacing=8,Padding=new Thickness(12,0)};
    readonly List<Border> tiles=[];
    readonly List<Image> thumbnails=[];
    readonly Button previous,next,zoomIn,zoomOut,fit,fullscreen,info,retry,cancel,stripPrevious,stripNext,back;
    CancellationTokenSource? galleryLifetime;
    CancellationToken loadToken;
    ChunkedMediaSource? videoSource;
    bool active,leaving,detailsOpen;
    readonly Action? restoreFocus;

    public PostDetailsPage(CardViewModel card,TelegramService? service,GroupInfo? group,bool demo,int initialIndex=0,string? topicTitle=null,Action? restoreFocus=null)
    {
        this.card=card;this.service=service;this.group=group;this.demo=demo;this.restoreFocus=restoreFocus;
        session=new(card.Post.Media.Count,initialIndex);Title="ดูสื่อ";BackgroundColor=Color.FromArgb("#101216");
        NavigationPage.SetHasNavigationBar(this,false);
        back=Icon("\uE72B","กลับไปหัวข้อ",async()=>await GoBack());
        previous=Icon("\uE76B","สื่อก่อนหน้า",async()=>await SelectMedia(session.Index-1));
        next=Icon("\uE76C","สื่อถัดไป",async()=>await SelectMedia(session.Index+1));
        zoomIn=Icon("\uE8A3","ซูมเข้า",()=>image.ZoomBy(1.25f));zoomOut=Icon("\uE71F","ซูมออก",()=>image.ZoomBy(.8f));
        fit=Icon("\uE9A6","ปรับภาพให้พอดีพื้นที่",()=>image.Fit());
        fullscreen=Icon("\uE740","เต็มจอ",ToggleFullscreen);info=Icon("\uE946","ข้อมูลโพสต์",ToggleDetails);
        retry=ActionButton("ลองอีกครั้ง",async()=>await LoadMedia());cancel=ActionButton("ยกเลิกการโหลด",CancelLoading);
        stripPrevious=Icon("\uE76B","เลื่อนภาพย่อไปทางซ้าย",async()=>await gallery.ScrollToAsync(Math.Max(0,gallery.ScrollX-gallery.Width*.8),0,true));
        stripNext=Icon("\uE76C","เลื่อนภาพย่อไปทางขวา",async()=>await gallery.ScrollToAsync(Math.Min(Math.Max(0,strip.Width-gallery.Width),gallery.ScrollX+gallery.Width*.8),0,true));
        var toolbar=new Grid{BackgroundColor=Surface,Padding=new Thickness(12,6),ColumnDefinitions={new(GridLength.Star),new(88),new(GridLength.Star)},ColumnSpacing=8};
        var titleRow=new Grid{ColumnDefinitions={new(40),new(GridLength.Star)},ColumnSpacing=8};titleRow.Add(back);
        var title=Text(topicTitle??group?.Title??"กลับไปหัวข้อ",14);title.LineBreakMode=LineBreakMode.TailTruncation;title.MaxLines=1;ToolTipProperties.SetText(title,title.Text);titleRow.Add(title,1);toolbar.Add(titleRow);
        counter.HorizontalTextAlignment=TextAlignment.Center;toolbar.Add(counter,1);
        var actions=new HorizontalStackLayout{Spacing=4,HorizontalOptions=LayoutOptions.End};zoomLabel.WidthRequest=44;zoomLabel.HorizontalTextAlignment=TextAlignment.Center;
        actions.Add(zoomOut);actions.Add(zoomLabel);actions.Add(zoomIn);actions.Add(fit);actions.Add(new BoxView{Color=Color.FromArgb("#363E4B"),WidthRequest=1,Margin=new Thickness(8,8)});actions.Add(fullscreen);actions.Add(info);
        if(!demo&&group!=null&&service?.PostUrl(group,card.Post.Id) is {} original)actions.Add(Icon("\uE712","เพิ่มเติม",()=>ShowMore(original)));
        toolbar.Add(actions,2);root.Add(toolbar);
        stage.Add(image);stage.Add(video);state.HorizontalTextAlignment=TextAlignment.Center;state.TextColor=Muted;
        status.Add(progress);status.Add(state);status.Add(retry);status.Add(cancel);stage.Add(status);
        previous.HorizontalOptions=LayoutOptions.Start;next.HorizontalOptions=LayoutOptions.End;previous.VerticalOptions=next.VerticalOptions=LayoutOptions.Center;previous.Margin=next.Margin=16;
        previous.WidthRequest=next.WidthRequest=44;previous.HeightRequest=next.HeightRequest=44;stage.Add(previous);stage.Add(next);
        mediaColumn.Add(stage);BuildStrip();gallery.Content=strip;filmstrip.Add(stripPrevious);filmstrip.Add(gallery,1);filmstrip.Add(stripNext,2);mediaColumn.Add(filmstrip,0,1);
        gallery.Scrolled+=(_,_)=>UpdateStripButtons();gallery.SizeChanged+=(_,_)=>{UpdateStripButtons();_=ScrollSelection();};strip.SizeChanged+=(_,_)=>UpdateStripButtons();
        body.Add(mediaColumn);BuildDetails(topicTitle);body.Add(details,1);root.Add(body,0,1);Content=root;SizeChanged+=(_,_)=>LayoutDetails();
        Loaded+=(_,_)=>{if(active)AttachKeyboard();};
        image.Ready+=()=>{if(active&&session.Complete(loadToken)){ShowState();UpdateTools();}};
        image.Failed+=()=>{if(active&&session.Fail(loadToken))ShowState();};
        image.ZoomChanged+=zoom=>{zoomLabel.Text=zoom<=1.01?"พอดี":$"{zoom:0.##}×";zoomOut.IsEnabled=zoom>1.01;zoomIn.IsEnabled=zoom<7.99;};
        video.Ready+=()=>{if(active&&video.IsVisible&&session.Complete(loadToken)){ShowState();UpdateTools();}};
        video.PlaybackFailed+=_=>{if(active&&video.IsVisible&&session.Fail(loadToken))ShowState();};
        video.BufferingChanged+=buffering=>{if(active&&video.IsVisible&&session.State==ViewerLoadState.Ready){status.IsVisible=buffering;progress.IsVisible=progress.IsRunning=buffering;state.Text="กำลังบัฟเฟอร์วิดีโอ…";cancel.IsVisible=retry.IsVisible=false;}};
        UpdateSelection();ShowState();
    }
    static Label Text(string text,double size)=>new(){Text=text,FontFamily="Leelawadee UI",FontSize=size,TextColor=Ink,VerticalOptions=LayoutOptions.Center};
    static Button ActionButton(string text,Action action)
    {
        var button=new Button{Text=text,FontFamily="Leelawadee UI",FontSize=13,HeightRequest=38,MinimumHeightRequest=0,MinimumWidthRequest=0,Padding=new Thickness(12,0),CornerRadius=7,BackgroundColor=Color.FromArgb("#283342"),TextColor=Ink,BorderWidth=0};
        var states=new VisualStateGroup{Name="CommonStates"};states.States.Add(new VisualState{Name="Normal"});
        var hover=new VisualState{Name="PointerOver"};hover.Setters.Add(new Setter{Property=Button.BackgroundColorProperty,Value=Color.FromArgb("#35455B")});states.States.Add(hover);
        var disabled=new VisualState{Name="Disabled"};disabled.Setters.Add(new Setter{Property=VisualElement.OpacityProperty,Value=.35});states.States.Add(disabled);
        // Native WinUI focus visuals remain active. MAUI stroke setters in Focused can run
        // after ButtonHandler tears down its platform view during navigation.
        states.States.Add(new VisualState{Name="Focused"});
        VisualStateManager.SetVisualStateGroups(button,new VisualStateGroupList{states});button.Clicked+=(_,_)=>action();return button;
    }
    static Button Icon(string glyph,string label,Action action)
    {
        var button=ActionButton(glyph,action);button.FontFamily="Segoe Fluent Icons";button.FontSize=17;button.WidthRequest=40;button.HeightRequest=40;button.Padding=0;
        SemanticProperties.SetDescription(button,label);ToolTipProperties.SetText(button,label);return button;
    }
    void BuildStrip()
    {
        for(var i=0;i<session.Count;i++)
        {
            var selected=i;var isVideo=card.Post.Media[i].Kind=="video";var thumbnail=new Image{Aspect=Aspect.AspectFill};thumbnails.Add(thumbnail);
            var content=new Grid();content.Add(new Label{Text=isVideo?"\uE714":"\uEB9F",FontFamily="Segoe Fluent Icons",TextColor=Muted,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center});content.Add(thumbnail);
            if(isVideo)content.Add(new Label{Text="\uE768",FontFamily="Segoe Fluent Icons",FontSize=13,TextColor=Colors.White,BackgroundColor=Color.FromArgb("#BB101216"),Padding=5,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.End,InputTransparent=true});
            var choose=ActionButton("",async()=>await SelectMedia(selected));choose.BackgroundColor=Colors.Transparent;choose.HeightRequest=-1;choose.HorizontalOptions=choose.VerticalOptions=LayoutOptions.Fill;
            SemanticProperties.SetDescription(choose,$"เลือก{(isVideo?"วิดีโอ":"ภาพ")} {i+1}");ToolTipProperties.SetText(choose,$"{(isVideo?"วิดีโอ":"ภาพ")} {i+1}");content.Add(choose);
            var tile=new Border{Content=content,WidthRequest=72,HeightRequest=72,Padding=2,StrokeThickness=2,StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=6},BackgroundColor=Color.FromArgb("#252C37")};tiles.Add(tile);strip.Add(tile);
        }
    }
    void BuildDetails(string? topic)
    {
        var heading=new Grid{ColumnDefinitions={new(GridLength.Star),new(40)}};heading.Add(Text("ข้อมูลโพสต์",15));heading.Add(Icon("\uE711","ปิดข้อมูลโพสต์",ToggleDetails),1);details.Add(heading);
        var content=new VerticalStackLayout{Spacing=16,Padding=new Thickness(0,8,0,24)};
        void Add(string label,string? value){if(string.IsNullOrWhiteSpace(value))return;var block=new VerticalStackLayout{Spacing=4};var caption=Text(label,12);caption.TextColor=Muted;block.Add(caption);block.Add(Text(value,14));content.Add(block);}
        Add("กลุ่ม",group?.Title);Add("หัวข้อ",topic);Add("วันที่โพสต์",card.Post.Date.ToLocalTime().ToString("dd MMM yyyy · HH:mm"));
        var photos=card.Post.Media.Count(x=>x.Kind!="video");var videos=card.Post.Media.Count-photos;
        Add("สื่อในโพสต์",string.Join(" · ",new[]{photos>0?$"ภาพ {photos}":null,videos>0?$"วิดีโอ {videos}":null}.OfType<string>()));
        fileInfo.TextColor=Muted;content.Add(fileInfo);Add("ข้อความ",card.Text);
        foreach(var link in card.Post.Links){var button=ActionButton(link,async()=>await Open(link));button.HorizontalOptions=LayoutOptions.Fill;ToolTipProperties.SetText(button,link);content.Add(button);}
        if(demo)Add("โหมดตัวอย่าง","สื่อตัวอย่างสำหรับตรวจหน้าตาโปรแกรม");details.Add(new ScrollView{Content=content},0,1);
    }
    void ToggleDetails(){detailsOpen=!detailsOpen;LayoutDetails();info.BackgroundColor=detailsOpen?Color.FromArgb("#214F83"):Color.FromArgb("#283342");}
    void LayoutDetails()
    {
        var overlay=Width<1000;details.IsVisible=detailsOpen;body.ColumnDefinitions[1].Width=detailsOpen&&!overlay?320:0;
        Grid.SetColumn(details,overlay?0:1);details.WidthRequest=overlay?Math.Min(320,Math.Max(240,Width-64)):-1;details.HorizontalOptions=overlay?LayoutOptions.End:LayoutOptions.Fill;details.ZIndex=10;
    }
    void UpdateSelection()
    {
        counter.Text=session.Count==0?"ไม่มีสื่อ":$"{session.Index+1} / {session.Count}";
        previous.IsVisible=next.IsVisible=filmstrip.IsVisible=session.Count>1;mediaColumn.RowDefinitions[1].Height=session.Count>1?96:0;
        previous.IsEnabled=session.HasPrevious;next.IsEnabled=session.HasNext;
        for(var i=0;i<tiles.Count;i++)tiles[i].Stroke=new SolidColorBrush(i==session.Index?Accent:Color.FromArgb("#394352"));
        fileInfo.Text=session.Count==0?"":$"รายการ {session.Index+1} · {(card.Post.Media[session.Index].Kind=="video"?"วิดีโอ":"ภาพ")}";UpdateTools();_=ScrollSelection();
    }
    void UpdateTools()
    {
        var photo=session.Count>0&&card.Post.Media[session.Index].Kind!="video";zoomIn.IsVisible=zoomOut.IsVisible=fit.IsVisible=zoomLabel.IsVisible=photo;
        zoomIn.IsEnabled=fit.IsEnabled=photo&&session.State==ViewerLoadState.Ready;if(session.State!=ViewerLoadState.Ready)zoomOut.IsEnabled=false;
    }
    void UpdateStripButtons(){stripPrevious.IsEnabled=gallery.ScrollX>1;stripNext.IsEnabled=gallery.ScrollX+gallery.Width<strip.Width-1;stripPrevious.IsVisible=stripNext.IsVisible=strip.Width>gallery.Width;}
    async Task ScrollSelection(){if(filmstrip.IsVisible&&session.Index<tiles.Count&&tiles[session.Index].Width>0)try{await gallery.ScrollToAsync(tiles[session.Index],ScrollToPosition.MakeVisible,false);}catch(InvalidOperationException){}}
    async Task SelectMedia(int index){if(session.Select(index))await LoadMedia();}
    void ShowState()
    {
        var loading=session.State==ViewerLoadState.Loading;progress.IsVisible=progress.IsRunning=loading;cancel.IsVisible=loading;
        retry.IsVisible=session.State is ViewerLoadState.Error or ViewerLoadState.Canceled;status.IsVisible=session.State!=ViewerLoadState.Ready;
        state.Text=session.Count==0?"โพสต์นี้ไม่มีไฟล์สื่อ":session.State switch{ViewerLoadState.Loading=>video.IsVisible?"กำลังเตรียมวิดีโอ…":"กำลังโหลดภาพ…",ViewerLoadState.Error=>"โหลดสื่อไม่สำเร็จ",ViewerLoadState.Canceled=>"ยกเลิกการโหลดแล้ว",_=>""};
    }
    void CancelLoading(){session.Cancel();image.FilePath=null;ReleaseVideo();ShowState();UpdateTools();}
    protected override async void OnAppearing(){base.OnAppearing();active=true;AttachKeyboard();galleryLifetime=new();_=LoadGalleryThumbnails(galleryLifetime.Token);await LoadMedia();}
    protected override void OnDisappearing()
    {
        active=false;galleryLifetime?.Cancel();galleryLifetime?.Dispose();galleryLifetime=null;session.Cancel();image.FilePath=null;ReleaseVideo();DetachKeyboard();ExitFullscreen();base.OnDisappearing();restoreFocus?.Invoke();
    }
    void ReleaseVideo(){video.StreamSource=null;video.FilePath=null;video.IsVisible=false;videoSource?.Dispose();videoSource=null;}
    async Task LoadGalleryThumbnails(CancellationToken ct)
    {
        for(var i=0;i<thumbnails.Count;i++)try
        {
            ct.ThrowIfCancellationRequested();var media=card.Post.Media[i];var path=demo?PreviewPath(media):service==null?null:await service.GetMediaAsync(media,false,ct);
            if(ct.IsCancellationRequested||!active)return;thumbnails[i].Source=path;
        }
        catch(OperationCanceledException){return;}
        catch(TL.RpcException e) when(TelegramRequestGate.IsFlood(e)){return;}
        catch{/* Keep selectable placeholders. */}
    }
    static string PreviewPath(MediaRef media)=>$"preview{(media.MessageId%6)+1}.jpg";
    async Task LoadMedia()
    {
        loadToken=session.Begin();var ct=loadToken;ReleaseVideo();image.FilePath=null;image.Fit();UpdateSelection();
        if(session.Count==0){ShowState();progress.IsVisible=progress.IsRunning=cancel.IsVisible=false;return;}
        var media=card.Post.Media[session.Index];var isVideo=media.Kind=="video";image.IsVisible=!isVideo;video.IsVisible=isVideo;ShowState();
        try
        {
            if(isVideo)
            {
                ChunkedMediaSource source;
                if(demo)
                {
                    await using var sample=await FileSystem.OpenAppPackageFileAsync("preview_video.mp4");using var memory=new MemoryStream();await sample.CopyToAsync(memory,ct);var bytes=memory.ToArray();
                    source=new ChunkedMediaSource(bytes.Length,"video/mp4",(offset,count,token)=>{token.ThrowIfCancellationRequested();return Task.FromResult(bytes.AsSpan((int)offset,Math.Min(count,bytes.Length-(int)offset)).ToArray());});
                }
                else{if(service==null)throw new InvalidOperationException();source=await service.OpenVideoAsync(media,ct);}
                if(!session.IsCurrent(ct)||!active){source.Dispose();return;}videoSource=source;video.StreamSource=source;
            }
            else
            {
                var path=demo?PreviewPath(media):service==null?null:await service.GetMediaAsync(media,true,ct);
                if(!session.IsCurrent(ct)||!active)return;if(path==null)throw new IOException();image.FilePath=path;
            }
        }
        catch(OperationCanceledException){}
        catch{if(active&&session.Fail(ct)){ShowState();UpdateTools();}}
    }
    async Task GoBack(){if(leaving)return;leaving=true;ExitFullscreen();await Navigation.PopAsync();}
    protected override bool OnBackButtonPressed(){_=GoBack();return true;}
    async Task Open(string url){if(LinkExtractor.IsSafe(url))try{await Launcher.Default.OpenAsync(url);}catch{await DisplayAlertAsync("เปิดลิงก์ไม่สำเร็จ","ตรวจสอบโปรแกรมที่ใช้เปิดลิงก์แล้วลองอีกครั้ง","ตกลง");}}
}
