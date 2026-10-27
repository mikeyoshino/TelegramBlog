using System.ComponentModel;
using TeleGramBlog.App.ViewModels;
using TeleGramBlog.Core;
using TeleGramBlog.Telegram;
namespace TeleGramBlog.App.Views;

public sealed class MediaCardView:ContentView
{
    static readonly SemaphoreSlim thumbnailQueue=new(1);
    readonly Image hero=new(){Aspect=Aspect.AspectFit};
    readonly Grid mediaArea=new(){BackgroundColor=Color.FromArgb("#F0F5FA")};
    readonly Label counter=new(){FontSize=11,TextColor=Colors.SlateGray,VerticalOptions=LayoutOptions.Center};
    readonly List<Border> tiles=[];
    readonly Dictionary<int,string?> paths=[];
    readonly List<Image> images=[];
    CardViewModel? card;
    CancellationTokenSource? lifetime;
    int selected;
    bool loaded;
    Func<MediaRef,CancellationToken,Task<string?>>? loader;
    public Func<MediaRef,CancellationToken,Task<string?>>? LoadThumbnail
    {get=>loader;set{loader=value;if(loaded&&card!=null)StartLoading();}}
    public event EventHandler? OpenRequested;
    public event EventHandler? LinkRequested;
    public int SelectedIndex=>selected;
    public CardViewModel? Card=>card;
    public MediaCardView()
    {
        mediaArea.SizeChanged+=(_,_)=>{if(mediaArea.Width>0)SetCardWidth(mediaArea.Width+2);};
        Loaded+=(_,_)=>{loaded=true;if(card!=null){card.PropertyChanged-=CardChanged;card.PropertyChanged+=CardChanged;CardChanged(null,new(""));}StartLoading();};
        Unloaded+=(_,_)=>{loaded=false;lifetime?.Cancel();if(card!=null)card.PropertyChanged-=CardChanged;};
    }
    protected override void OnBindingContextChanged()
    {
        if(card!=null)card.PropertyChanged-=CardChanged;
        lifetime?.Cancel();lifetime?.Dispose();lifetime=null;
        base.OnBindingContextChanged();card=BindingContext as CardViewModel;
        if(mediaArea.Parent is Layout oldBody)oldBody.Remove(mediaArea);
        if(counter.Parent is Layout oldRow)oldRow.Remove(counter);
        selected=0;paths.Clear();tiles.Clear();images.Clear();mediaArea.Children.Clear();hero.Source=null;
        if(card==null){Content=null;return;}
        card.PropertyChanged+=CardChanged;
        var body=new VerticalStackLayout {Spacing=0};
        mediaArea.Add(hero);
        if(card.Post.Media.Count==0)
            mediaArea.Add(new Label {Text=card.Text.Length>0?card.Text:card.Title,Margin=20,MaxLines=8,LineBreakMode=LineBreakMode.TailTruncation,TextColor=Color.FromArgb("#13243A")});
        var open=new Button {Text="",BackgroundColor=Colors.Transparent,BorderWidth=0,CornerRadius=0,HorizontalOptions=LayoutOptions.Fill,VerticalOptions=LayoutOptions.Fill};
        SemanticProperties.SetDescription(open,card.ViewLabel);open.Clicked+=(_,_)=>OpenRequested?.Invoke(this,EventArgs.Empty);mediaArea.Add(open);
        if(card.HasBadge)
            mediaArea.Add(new Border {InputTransparent=true,Content=new Label {Text=card.Badge,FontSize=11,TextColor=Colors.White},BackgroundColor=Color.FromArgb("#CC13243A"),StrokeThickness=0,Padding=new Thickness(8,5),Margin=10,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Start});
        body.Add(mediaArea);
        if(card.Post.Media.Count>1)
        {
            var previous=new Button {Text="‹",WidthRequest=32,HeightRequest=36,Padding=0,BackgroundColor=Color.FromArgb("#AA13243A"),TextColor=Colors.White,HorizontalOptions=LayoutOptions.Start,VerticalOptions=LayoutOptions.Center,Margin=8};
            var next=new Button {Text="›",WidthRequest=32,HeightRequest=36,Padding=0,BackgroundColor=Color.FromArgb("#AA13243A"),TextColor=Colors.White,HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.Center,Margin=8};
            SemanticProperties.SetDescription(previous,"Previous preview");SemanticProperties.SetDescription(next,"Next preview");
            previous.Clicked+=async(_,_)=>await SelectAsync((selected-1+card.Post.Media.Count)%card.Post.Media.Count);
            next.Clicked+=async(_,_)=>await SelectAsync((selected+1)%card.Post.Media.Count);
            mediaArea.Add(previous);mediaArea.Add(next);
            var strip=new HorizontalStackLayout {Spacing=6};
            for(var i=0;i<card.Post.Media.Count;i++)
            {
                var item=i;var video=card.Post.Media[i].Kind=="video";
                var image=new Image {Aspect=Aspect.AspectFill};images.Add(image);
                var grid=new Grid();grid.Add(new Label {Text=video?"▶":"▧",HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center,TextColor=Colors.SlateGray});grid.Add(image);
                if(video)grid.Add(new Label {Text="▶",InputTransparent=true,TextColor=Colors.White,BackgroundColor=Color.FromArgb("#AA13243A"),HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center,Padding=3});
                var choose=new Button {Text="",BackgroundColor=Colors.Transparent,Padding=0};
                SemanticProperties.SetDescription(choose,$"Preview {(video?"video":"photo")} {i+1}");choose.Clicked+=async(_,_)=>await SelectAsync(item);grid.Add(choose);
                var tile=new Border {Content=grid,WidthRequest=58,HeightRequest=48,Padding=1,StrokeThickness=2,BackgroundColor=Color.FromArgb("#EFF6FD")};tiles.Add(tile);strip.Add(tile);
            }
            var galleryRow=new Grid {ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)},ColumnSpacing=8,Padding=new Thickness(8,8)};
            galleryRow.Add(new ScrollView {Orientation=ScrollOrientation.Horizontal,HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Content=strip,HeightRequest=52});galleryRow.Add(counter,1);body.Add(galleryRow);
        }
        var footer=new Grid {ColumnDefinitions={new(GridLength.Star),new(GridLength.Auto)},Padding=new Thickness(10,2),HeightRequest=34};
        footer.Add(new Label {Text=card.DateLabel,FontSize=11,TextColor=Colors.SlateGray,VerticalOptions=LayoutOptions.Center});
        if(card.HasLink){var link=new Button {Text="↗",FontSize=19,Padding=0,WidthRequest=30,HeightRequest=30,BackgroundColor=Colors.Transparent};SemanticProperties.SetDescription(link,"Open link");link.Clicked+=(_,_)=>LinkRequested?.Invoke(this,EventArgs.Empty);footer.Add(link,1);}
        body.Add(footer);Content=new Border {Content=body,Padding=0,Stroke=Color.FromArgb("#E3EAF2"),StrokeThickness=1,StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {CornerRadius=10}};
        UpdateSelection();CardChanged(null,new(""));Resize();if(loaded)StartLoading();
    }
    void Resize()
    {
        if(Width<=0||card==null)return;
        SetCardWidth(Width);
    }
    public void SetCardWidth(double width)
    {
        if(width<=0||card==null)return;
        var height=card.Post.Media.Count==0?200:(width-2)*1.25;
        if(Math.Abs(mediaArea.HeightRequest-height)>1)
        {
            mediaArea.HeightRequest=height;hero.HeightRequest=height;
            InvalidateMeasure();
        }
        // Recycled cells can change between single media and albums at the same width.
        var required=RequiredHeight(width,card.Post.Media.Count);
        if(Math.Abs(HeightRequest-required)>1){HeightRequest=required;InvalidateMeasure();}
    }
    public static double RequiredHeight(double width,int mediaCount)=>(mediaCount==0?200:(width-2)*1.25)+(mediaCount>1?68:0)+36;
    void CardChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(card?.ImagePath is not {} path)return;
        paths[0]=path;if(images.Count>0)images[0].Source=path;if(selected==0)hero.Source=path;
    }
    void UpdateSelection()
    {
        hero.Source=paths.GetValueOrDefault(selected);counter.Text=$"{selected+1} / {card?.Post.Media.Count}";
        for(var i=0;i<tiles.Count;i++)tiles[i].Stroke=new SolidColorBrush(Color.FromArgb(i==selected?"#087DFA":"#E3EAF2"));
    }
    async Task SelectAsync(int item)
    {
        selected=item;UpdateSelection();if(lifetime!=null)await LoadOne(item,lifetime.Token);
    }
    void StartLoading()
    {
        lifetime?.Cancel();lifetime?.Dispose();lifetime=new();_=LoadStrip(lifetime.Token);
    }
    async Task LoadStrip(CancellationToken ct)
    {
        if(card?.Post.Media.Count>0&&!await LoadOne(selected,ct))return;
        // At most two extra previews per visible card; other items load when selected.
        for(var i=1;i<Math.Min(3,images.Count)&&!ct.IsCancellationRequested;i++)
            if(!await LoadOne(i,ct))return;
    }
    async Task<bool> LoadOne(int item,CancellationToken ct)
    {
        var current=card;
        if(current==null||LoadThumbnail==null||paths.ContainsKey(item))return true;
        try
        {
            await thumbnailQueue.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                if(paths.ContainsKey(item))return true;
                var path=await LoadThumbnail(current.Post.Media[item],ct);
                if(ct.IsCancellationRequested||current!=card)return false;
                if(path!=null){paths[item]=path;if(item<images.Count)images[item].Source=path;if(selected==item)hero.Source=path;}
                await Task.Delay(300,ct);
                return true;
            }
            finally{thumbnailQueue.Release();}
        }
        catch(OperationCanceledException){return false;}
        catch(TL.RpcException e) when(TelegramRequestGate.IsFlood(e)){return false;}
        catch(Exception){return false;}
    }
}
