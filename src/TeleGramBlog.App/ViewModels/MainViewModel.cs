using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TeleGramBlog.Core;
using TeleGramBlog.Telegram;

namespace TeleGramBlog.App.ViewModels;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    protected void AllChanged()=>Changed("");
}
public sealed class CardViewModel(PostCard post) : Observable
{
    public PostCard Post {get;}=post;
    public string Title=>Post.Title;
    public string Text=>Post.Text;
    public string Domain=>Post.Domain;
    public string DateLabel=>Post.DateLabel;
    public string Badge
    {
        get
        {
            var videos=Post.Media.Count(m=>m.Kind=="video");var photos=Post.Media.Count-videos;
            if(videos==0)return photos>1?$"{photos} photos":"";
            return (photos>0?$"{photos} photos · ":"")+$"▶ {videos} video{(videos==1?"":"s")}";
        }
    }
    public string ViewLabel=>Post.Media.Count>1?"View album":Post.Media.FirstOrDefault()?.Kind=="video"?"View video":Post.Media.Count>0?"View image":"View post";
    public bool HasBadge=>Badge.Length>0;
    public bool HasLink=>Post.Links.Count>0;
    public string Placeholder=>Post.Media.Count>0?"Image preview":"↗";
    string? imagePath;
    public string? ImagePath {get=>imagePath;set {imagePath=value;AllChanged();}}
    public bool HasImage=>imagePath!=null;
}

public sealed class MainViewModel : Observable, IDisposable
{
    public ObservableCollection<GroupInfo> Groups {get;}=[];
    public bool IncludeTextMessages { get; init; }
    public bool MediaOnly { get; init; }
    public bool PrefetchThumbnails { get; init; } = true;
    public ObservableCollection<GroupInfo> RailGroups {get;}=[];
    public ObservableCollection<TopicInfo> Topics {get;}=[];
    public ObservableCollection<CardViewModel> Cards {get;}=[];
    public ObservableCollection<int> PageNumbers {get;}=[];
    List<GroupInfo> allGroups=[];
    List<TopicInfo> allTopics=[];
    readonly NavigationScope navigation=new();
    readonly string root;
    TelegramService? service;
    HistoryPager? pager;
    bool initialized;
    bool historyLoaded;
    bool groupsLoaded,topicsLoaded;
    bool canceled;
    bool cached;
    HistoryPager? refreshFallback;
    readonly HistoryPagerCache rememberedPagers=new();
    readonly Dictionary<(long,int),int> rememberedPages=[];
    int sortIndex;
    readonly Dictionary<long,string> topicQueries=[];
    readonly Dictionary<(long,int),string> postQueries=[];
    readonly CancellationTokenSource avatarsLifetime=new();
    public bool IsConnected=>connected;
    public bool ShowGroupsEmpty=>IsGroupPicker&&groupsLoaded&&!busy&&!HasError&&!canceled&&Groups.Count==0;
    public string GroupsEmptyText=>string.IsNullOrWhiteSpace(groupQuery)?"ยังไม่มีกลุ่มที่แสดงได้":"ไม่พบกลุ่มที่ตรงกับคำค้น";
    public bool ShowTopicsEmpty=>IsOverview&&topicsLoaded&&!busy&&!HasError&&!canceled&&Topics.Count==0;
    public bool IsOverview=>IsWorkspace&&SelectedTopic==null;
    public bool IsPosts=>IsWorkspace&&SelectedTopic!=null;
    public bool HasTopicSidebar=>IsWorkspace&&SelectedGroup?.IsForum==true;
    public bool ShowMediaEmpty=>IsPosts&&historyLoaded&&!busy&&!HasError&&!canceled&&Cards.Count==0&&string.IsNullOrWhiteSpace(postQuery);
    public bool ShowSearchEmpty=>IsPosts&&historyLoaded&&!busy&&!HasError&&!canceled&&Cards.Count==0&&!string.IsNullOrWhiteSpace(postQuery);
    public bool ShowPagination=>IsPosts&&Cards.Count>0;
    public bool ShowPageNavigation=>ShowPagination&&string.IsNullOrWhiteSpace(postQuery)&&(page>1||hasNext);
    public bool ShowCanceled=>IsConnected&&canceled&&!busy&&!HasError&&(IsGroupPicker?Groups.Count==0:IsOverview?Topics.Count==0:Cards.Count==0);
    public bool ShowFirstLoading=>busy&&(IsGroupPicker?Groups.Count==0:IsOverview?Topics.Count==0:Cards.Count==0);
    public bool ShowBusyStrip=>busy&&!ShowFirstLoading;
    public string LoadingText=>IsGroupPicker?"กำลังโหลดรายการกลุ่ม…":IsOverview?"กำลังโหลดหัวข้อ…":Cards.Count>0?"กำลังอัปเดตโพสต์…":"กำลังโหลดโพสต์…";
    public string LocationLabel=>IsGroupPicker?"กลุ่มของฉัน":IsOverview?"ภาพรวมกลุ่ม":SelectedGroup?.Title??"กลุ่มของฉัน";
    public int LoadedCount=>pager?.LoadedCards.Count??0;
    public string LoadedLabel=>$"{LoadedCount} โพสต์ที่โหลดแล้ว";
    public string SyncLabel=>cached?"แสดงข้อมูลที่บันทึกไว้"+(pager?.FetchedAt is {} time?$" · อัปเดตล่าสุด {time.LocalDateTime:dd/MM/yyyy HH:mm}":""):
        pager?.FetchedAt is {} updated?$"อัปเดตล่าสุด {updated.LocalDateTime:dd/MM/yyyy HH:mm}":"";
    public IReadOnlyList<string> SortOptions {get;}=["ใหม่ก่อน · รายการที่แสดง","เก่าก่อน · รายการที่แสดง"];
    public int SortIndex {get=>sortIndex;set{sortIndex=value;FilterPosts();}}
    public string SelectedSort {get=>SortOptions[Math.Clamp(sortIndex,0,1)];set{if(value==null)return;sortIndex=value==SortOptions[1]?1:0;FilterPosts();}}
    readonly Func<GroupInfo,TopicInfo,IHistorySource>? historySourceFactory;

    bool connected;
    bool busy;
    bool loggingOut;
    bool qrMode=true;
    int authGeneration;
    readonly QrLoginFlow qrLogin=new();
    readonly Action<Action> dispatch;
    string? renderedQr;
    public byte[]? QrPng {get;private set;}
    public bool ShowQrPanel=>qrMode&&!qrLogin.NeedsPassword;
    public bool ShowLoginInput=>!qrMode||qrLogin.NeedsPassword;
    public bool ShowQrImage=>QrPng!=null;
    public bool ShowQrPlaceholder=>QrPng==null;
    public bool CanSubmitLogin=>!busy||qrLogin.NeedsPassword;
    public bool ShowConnectButton=>!qrMode||!busy||qrLogin.NeedsPassword;
    public string SwitchLoginLabel=>qrMode?"Log in with phone number":"Log in with QR code";
    public string QrStatus=>busy?"Waiting for you to scan · QR refreshes automatically":"Click Connect Telegram to get your QR code";
    public MainViewModel(Action<Action>? dispatch=null,string? dataRoot=null,Func<GroupInfo,TopicInfo,IHistorySource>? historySourceFactory=null)
    {
        this.historySourceFactory=historySourceFactory;
        root=dataRoot??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TeleGramBlog");
        this.dispatch=dispatch??(action=>action());
        qrLogin.Changed+=()=>this.dispatch(()=>{
            if(renderedQr!=qrLogin.Url){renderedQr=qrLogin.Url;QrPng=renderedQr==null?null:LoginQrImage.Encode(renderedQr);}
            AllChanged();
        });
    }
    bool showGroups;
    bool demo;
    int page=1;
    int pageSize=24;
    bool hasNext;
    int knownPages=1;
    string loginStep="phone_number";
    string status="Your groups. A clearer view.";
    string error="";
    string groupQuery="",topicQuery="",postQuery="";
    public string ApiId {get;set;}="";
    public string ApiHash {get;set;}="";
    public string LoginInput {get;set;}="";
    public GroupInfo? SelectedGroup {get;private set;}
    public TopicInfo? SelectedTopic {get;private set;}
    public bool IsLogin=>!connected;
    public bool IsGroupPicker=>connected && showGroups;
    public bool IsWorkspace=>connected && !showGroups;
    public bool IsDemo=>demo;
    public bool IsBusy=>busy;
    public bool IsReady=>!busy;
    public bool CanPrevious=>!busy && page>1;
    public bool HasPreviousPage=>page>1;
    public bool HasNextPage=>hasNext&&string.IsNullOrWhiteSpace(postQuery);
    public bool CanNext=>!busy && hasNext && string.IsNullOrWhiteSpace(postQuery);
    public bool HasError=>error.Length>0;
    public bool IsEmpty=>!busy && Cards.Count==0 && !HasError;
    public bool NoGroups=>!busy && Groups.Count==0;
    public bool NoTopics=>!busy && Topics.Count==0;
    public bool PasswordInput=>qrLogin.NeedsPassword||loginStep=="password";
    public string LoginTitle=>qrLogin.NeedsPassword?"Two-step verification":loginStep switch {"verification_code"=>"Check your Telegram", "password"=>"One more step", "email"=>"Verify with email", "email_verification_code"=>"Check your email", _=>"Your Telegram, beautifully organised."};
    public string LoginDescription=>qrLogin.NeedsPassword?"Enter your Telegram two-step verification password to finish connecting. If asked again, check your password and retry.":loginStep switch {"verification_code"=>"Enter the verification code sent by Telegram.","password"=>"Enter your Telegram two-step verification password.","email"=>"Telegram requests an email address for verification.","email_verification_code"=>"Enter the code Telegram sent to your email.",_=>"Connect your account to browse your groups, topics and saved inspiration in one calm workspace."};
    public string InputHint=>qrLogin.NeedsPassword?"Two-step verification password":loginStep switch {"verification_code" or "email_verification_code"=>"Verification code", "password"=>"Two-step verification password", "email"=>"Email address", _=>"Phone number, including +country code"};
    public string ConnectLabel=>qrLogin.NeedsPassword?"Continue":loginStep=="phone_number"?"Connect Telegram   →":"Continue   →";
    public string Status=>status;
    public string Error=>error;
    public string GroupTitle=>SelectedGroup?.Title ?? "Your groups";
    public string TopicTitle=>SelectedTopic?.Title ?? "Select a topic";
    public string TopicCount=>!topicsLoaded&&busy?"กำลังโหลดหัวข้อ…":$"{allTopics.Count} หัวข้อ";
    public string GroupCount=>!groupsLoaded&&busy?"กำลังโหลดรายการ…":$"{allGroups.Count} กลุ่ม";
    public string AccountName=>demo?"Design preview":service?.AccountName ?? "Telegram";
    public string ConnectionLabel=>demo?"ตัวอย่าง · ไม่ได้เชื่อม Telegram":"เชื่อมต่อ Telegram แล้ว";
    public string PageLabel=>$"หน้า {page} · โหลดแล้ว {knownPages} หน้า";
    public string PageSummary=>postQuery.Length>0?$"พบ {Cards.Count} จาก {LoadedCount} โพสต์ที่โหลดแล้ว":Cards.Count==0?"":$"แสดง {(page-1)*pageSize+1}–{(page-1)*pageSize+Cards.Count} จาก {LoadedCount} โพสต์ที่โหลดแล้ว";
    public string GroupQuery {get=>groupQuery;set {groupQuery=value;FilterGroups();}}
    public string TopicQuery {get=>topicQuery;set {topicQuery=value;FilterTopics();}}
    public string PostQuery
    {
        get=>postQuery;
        set
        {
            postQuery=value;if(SelectedGroup!=null&&SelectedTopic!=null)postQueries[(SelectedGroup.Id,SelectedTopic.Id)]=value;FilterPosts();
            if(demo){foreach(var card in Cards)card.ImagePath=$"preview{(card.Post.Id%6)+1}.jpg";}
            else if(service!=null)_=LoadThumbnailsAsync(Cards.Where(c=>!c.HasImage).ToArray(),navigation.Token);
        }
    }
    public IReadOnlyList<int> PageSizes {get;}=[24,48];
    public int PageSize {get=>pageSize;set {if(busy||pageSize==value)return;pageSize=value;rememberedPages.Clear();Changed();}}
    List<CardViewModel> currentCards=[];
    public TelegramService? Service=>service;
    // Read-only state for the iOS presentation adapter; the desktop remains the source of behavior.
    public int CurrentPage=>page;
    public int KnownPages=>knownPages;
    public bool IsCached=>cached;
    public DateTimeOffset? HistoryFetchedAt=>pager?.FetchedAt;
    public async Task UseConnectedServiceAsync(TelegramService connectedService, bool loadGroups = true)
    {
        if(connectedService.AccountId==0)throw new InvalidOperationException("Authenticate before opening the workspace.");
        if(service!=null&&service!=connectedService)throw new InvalidOperationException("Disconnect the previous account first.");
        service=connectedService;connected=true;showGroups=true;demo=false;initialized=true;
        busy=true;error="";AllChanged();
        try{if(loadGroups)await LoadGroupsAsync(navigation.Next());}
        catch(Exception e){error=ExplainShell(e);}
        finally{busy=false;AllChanged();}
    }

    static void Replace<T>(ObservableCollection<T> target,IEnumerable<T> source) {target.Clear();foreach(var item in source)target.Add(item);}
    void FilterGroups(){Replace(Groups,allGroups.Where(g=>g.Title.Contains(groupQuery,StringComparison.CurrentCultureIgnoreCase)));AllChanged();}
    void FilterTopics(){Replace(Topics,allTopics.Where(t=>t.Title.Contains(topicQuery,StringComparison.CurrentCultureIgnoreCase)));AllChanged();}
    void FilterPosts()
    {
        IEnumerable<CardViewModel> filtered=string.IsNullOrWhiteSpace(postQuery)?currentCards:
            (pager?.LoadedCards??[]).Where(p=>p.Text.Contains(postQuery,StringComparison.CurrentCultureIgnoreCase)||p.Links.Any(l=>l.Contains(postQuery,StringComparison.OrdinalIgnoreCase))).Select(p=>currentCards.FirstOrDefault(c=>c.Post.Id==p.Id)??new CardViewModel(p));
        Replace(Cards,sortIndex==1?filtered.OrderBy(c=>c.Post.Id):filtered.OrderByDescending(c=>c.Post.Id));
        AllChanged();
    }
    public async Task InitializeAsync()
    {
        if(initialized)return;initialized=true;
        try
        {
            TelegramConfig? config=null;
            var protectedConfig=new ProtectedSessionStore(Path.Combine(root,"app.config"));
            if(protectedConfig.Load() is {} bytes) config=JsonSerializer.Deserialize<TelegramConfig>(bytes);
            var file=Path.Combine(AppContext.BaseDirectory,"telegram.json");
            if(config==null && File.Exists(file)) config=JsonSerializer.Deserialize<TelegramConfig>(File.ReadAllText(file),new JsonSerializerOptions {PropertyNameCaseInsensitive=true});
            if(int.TryParse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID"),out var id)) config=new(id,Environment.GetEnvironmentVariable("TELEGRAM_API_HASH")??"");
            if(config?.IsValid==true) {ApiId=config.ApiId.ToString();ApiHash=config.ApiHash;AllChanged();
                service=new(config,root); if(service.HasSession)await ConnectAsync();}
        }
        catch(Exception e) {error="Saved connection could not be opened. Restart sign-in or check the application setup guide. "+ExplainShell(e);AllChanged();}
    }
    public async Task ConnectAsync()
    {
        if(qrLogin.NeedsPassword){qrLogin.SubmitPassword(LoginInput);LoginInput="";AllChanged();return;}
        if(qrMode && service?.HasSession!=true){await StartQrAsync();return;}
        if(busy)return;busy=true;error="";AllChanged();
        var generation=authGeneration;
        try
        {
            EnsureService();var activeService=service!;
            if(loginStep=="phone_number" && !activeService.HasSession && string.IsNullOrWhiteSpace(LoginInput))throw new ArgumentException("Enter your phone number with country code.");
            var next=await activeService.LoginAsync(LoginInput);
            if(generation!=authGeneration)return;
            LoginInput="";
            if(next!=null)
            {
                if(next is not ("verification_code" or "password" or "email" or "email_verification_code"))
                    throw new InvalidOperationException("This login requires an action in the official Telegram app. Finish account setup there, then restart sign-in.");
                loginStep=next;
                qrMode=false;
                status="Complete verification to connect.";
            }
            else {connected=true;showGroups=true;status="Connected · choose a group";await LoadGroupsAsync(navigation.Next());}
        }
        catch(Exception e)
        {
            if(generation==authGeneration)
            {
                if(e is TL.RpcException {Code:401})
                {
                    RecoverExpiredSession();
                }
                else error=e is AppNotConfiguredException?e.Message:ExplainShell(e);
            }
        }
        finally{if(generation==authGeneration){busy=false;AllChanged();}}
    }
    static string ExplainShell(Exception e)=>e switch
    {
        TL.RpcException r when TelegramRequestGate.IsFlood(r)=>$"Telegram ขอให้รอ {Math.Max(1,r.X)} วินาที แล้วลองใหม่",
        TL.RpcException {Code:401}=>"การเชื่อมต่อหมดอายุ กรุณาเข้าสู่ระบบใหม่",
        _=>"โหลดข้อมูลไม่สำเร็จ ตรวจสอบการเชื่อมต่อแล้วลองใหม่"
    };
    void EnsureService()
    {
        if(service!=null)return;
        if(!int.TryParse(ApiId,out var id)||!new TelegramConfig(id,ApiHash.Trim()).IsValid)throw new AppNotConfiguredException();
        service=new(new(id,ApiHash.Trim()),root);
    }
    async Task StartQrAsync()
    {
        if(busy)return;
        var generation=authGeneration;busy=true;error="";AllChanged();
        try
        {
            EnsureService();var activeService=service!;
            await qrLogin.RunAsync(activeService.LoginWithQrAsync);
            if(generation!=authGeneration)return;
            connected=true;showGroups=true;status="Connected · choose a group";
            await LoadGroupsAsync(navigation.Next());
        }
        catch(OperationCanceledException){}
        catch(Exception e)
        {
            if(generation==authGeneration)
            {
                if(e is TL.RpcException {Code:401})RecoverExpiredSession();
                else error=e is AppNotConfiguredException?e.Message:ExplainShell(e);
            }
        }
        finally{if(generation==authGeneration){busy=false;AllChanged();}}
    }
    public void SwitchLoginMode()
    {
        if(connected)return;
        CancelAuthentication();qrMode=!qrMode;loginStep="phone_number";LoginInput="";error="";AllChanged();
    }
    internal void RecoverExpiredSession()
    {
        rememberedPagers.Clear();rememberedPages.Clear();postQueries.Clear();topicQueries.Clear();refreshFallback=null;
        groupsLoaded=false;topicsLoaded=false;
        CancelAuthentication();navigation.Next();connected=false;showGroups=false;demo=false;qrMode=true;loginStep="phone_number";LoginInput="";
        Groups.Clear();RailGroups.Clear();Topics.Clear();Cards.Clear();allGroups=[];allTopics=[];currentCards=[];pager=null;SelectedGroup=null;SelectedTopic=null;
        try{new ProtectedSessionStore(Path.Combine(root,"account.session")).Clear();error="Your previous session has expired. Click Connect Telegram to sign in again.";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){error="The expired session could not be removed. Close the app and remove account.session from %LOCALAPPDATA%/TeleGramBlog before signing in again.";}
        AllChanged();
    }
    void CancelAuthentication()
    {
        authGeneration++;qrLogin.Stop();service?.Dispose();service=null;QrPng=null;renderedQr=null;busy=false;
    }
    async Task LoadGroupsAsync(CancellationToken ct)
    {
        var groups=await service!.GetGroupsAsync(ct);
        if(!navigation.IsCurrent(ct))return;
        groupsLoaded=true;allGroups=groups.ToList();Replace(RailGroups,allGroups);FilterGroups();
        _=LoadGroupAvatarsAsync(allGroups.ToArray(),service!,avatarsLifetime.Token);
    }
    async Task LoadGroupAvatarsAsync(GroupInfo[] groups,TelegramService active,CancellationToken ct)
    {
        foreach(var group in groups)
        {
            try{var path=await active.GetGroupAvatarAsync(group.Id,ct);if(ct.IsCancellationRequested||service!=active)return;group.AvatarPath=path;}
            catch(OperationCanceledException){return;}
            catch(TL.RpcException e) when(TelegramRequestGate.IsFlood(e)){return;}
            catch(Exception){if(service!=active)return;}
        }
    }
    public void BackToOverview()
    {
        if(SelectedGroup?.IsForum!=true)return;
        navigation.Next();rememberedPagers.Trim();refreshFallback=null;busy=false;showGroups=false;SelectedTopic=null;pager=null;Cards.Clear();currentCards=[];error="";historyLoaded=false;canceled=false;AllChanged();
    }
    public Task RetryAsync()=>IsGroupPicker?RefreshAsync():IsOverview&&SelectedGroup!=null?SelectGroupAsync(SelectedGroup):SelectedTopic!=null?SelectTopicAsync(SelectedTopic,true):Task.CompletedTask;
    public async Task SelectGroupAsync(GroupInfo group)
    {
        if(loggingOut)return;
        if(SelectedGroup!=null)topicQueries[SelectedGroup.Id]=topicQuery;
        historyLoaded=false;topicsLoaded=false;canceled=false;cached=false;
        var ct=navigation.Next();rememberedPagers.Trim();refreshFallback=null;busy=true;error="";showGroups=false;SelectedGroup=group;SelectedTopic=null;pager=null;
        foreach(var item in RailGroups)item.IsSelected=item.Id==group.Id;
        currentCards=[];Cards.Clear();Topics.Clear();allTopics=[];page=1;knownPages=1;hasNext=false;PageNumbers.Clear();AllChanged();
        try
        {
            var topics=demo&&!group.IsForum?new List<TopicInfo>{new(0,"โพสต์รวม")}:demo?new List<TopicInfo>{new(1,"General"),new(2,"Design inspiration"),new(3,"Useful tools"),new(4,"Photography"),new(5,"Reading list"),new(6,"Development")}:await service!.GetTopicsAsync(group,ct);
            if(!navigation.IsCurrent(ct))return;
            topicsLoaded=true;allTopics=topics.ToList();topicQuery=topicQueries.GetValueOrDefault(group.Id,"");FilterTopics();
            if(group.IsForum)group.KnownTopicCount=allTopics.Count;
            if(!group.IsForum&&allTopics.Count>0) await SelectTopicAsync(allTopics[0]);
        }
        catch(OperationCanceledException){}
        catch(Exception e){if(navigation.IsCurrent(ct))error=ExplainShell(e);}
        finally{if(navigation.IsCurrent(ct)){busy=false;AllChanged();}}
    }
    public async Task SelectTopicAsync(TopicInfo topic,bool refresh=false)
    {
        if(loggingOut)return;
        if(SelectedGroup==null)return;
        var sameTopic=SelectedTopic?.Id==topic.Id;
        var key=(SelectedGroup.Id,topic.Id);
        var oldPager=sameTopic?pager:rememberedPagers.GetValueOrDefault(key);
        var targetPage=refresh?1:rememberedPages.GetValueOrDefault(key,1);
        refreshFallback=refresh?oldPager:null;
        var ct=navigation.Next();busy=true;error="";canceled=false;historyLoaded=false;SelectedTopic=topic;
        page=targetPage;knownPages=1;hasNext=false;PageNumbers.Clear();postQuery=postQueries.GetValueOrDefault((SelectedGroup.Id,topic.Id),"");
        if(!sameTopic){pager=null;currentCards=[];Cards.Clear();}AllChanged();
        try
        {
            var snapshot=!demo && !refresh && oldPager==null?service!.Store.ReadHistory(service.AccountId,SelectedGroup.Id,topic.Id):null;
            cached=snapshot!=null||(!refresh&&oldPager!=null);
            pager=!refresh&&oldPager!=null?oldPager:new(historySourceFactory?.Invoke(SelectedGroup,topic)??(demo?new DemoHistorySource():service!.CreateHistorySource(SelectedGroup,topic)),snapshot,IncludeTextMessages,MediaOnly);
            var cachedPage=(snapshot!=null||!refresh?pager:oldPager)?.GetCachedPage(targetPage,pageSize);
            if(cachedPage!=null)DisplayPage(cachedPage,ct);
            status=demo?"Preview mode · sample posts":snapshot==null?"Loading history…":"Cached history · Sync to check for updates";
            await LoadPageCoreAsync(targetPage,ct);refreshFallback=null;
        }
        catch(OperationCanceledException){}
        catch(Exception e)
        {
            if(navigation.IsCurrent(ct))
            {
                var partial=pager?.GetCachedPage(1,pageSize);
                if(partial?.Cards.Count>0)DisplayPage(partial,ct);
                else if(oldPager!=null)pager=oldPager;
                refreshFallback=null;
                cached=Cards.Count>0;
                if(!demo&&pager!=null)
                    try{service!.Store.SaveHistory(service.AccountId,SelectedGroup.Id,topic.Id,pager.Snapshot);}catch(IOException){}
                error=ExplainShell(e);
                status=Cards.Count>0?"Unable to update · showing saved posts":"Unable to load posts · see the message below";
            }
        }
        finally{if(navigation.IsCurrent(ct)){busy=false;AllChanged();}}
    }
    public async Task ChangePageAsync(int requested)
    {
        if(busy || pager==null || requested<1)return;
        var ct=navigation.Next();busy=true;error="";AllChanged();
        try{await LoadPageCoreAsync(requested,ct);}
        catch(OperationCanceledException){}
        catch(Exception e){if(navigation.IsCurrent(ct))error=ExplainShell(e);}
        finally{if(navigation.IsCurrent(ct)){busy=false;AllChanged();}}
    }
    async Task LoadPageCoreAsync(int requested,CancellationToken ct)
    {
        var activePager=pager!;
        var result=await activePager.GetPageAsync(requested,pageSize,ct);
        if(!navigation.IsCurrent(ct))return;
        if(!demo)service!.Store.SaveHistory(service.AccountId,SelectedGroup!.Id,SelectedTopic!.Id,activePager.Snapshot);
        historyLoaded=true;canceled=false;
        if(SelectedGroup!=null&&SelectedTopic!=null)rememberedPagers[(SelectedGroup.Id,SelectedTopic.Id)]=activePager;
        DisplayPage(result,ct);
        if(!status.StartsWith("Cached"))status=demo?"Preview mode · sample posts":$"{result.LoadedCount} posts loaded · Sync for latest";
    }
    void DisplayPage(PostPage result,CancellationToken ct)
    {
        page=result.Page;hasNext=result.HasNext;knownPages=result.KnownPages;
        if(SelectedGroup!=null&&SelectedTopic!=null)rememberedPages[(SelectedGroup.Id,SelectedTopic.Id)]=page;
        currentCards=result.Cards.Select(p=>new CardViewModel(p)).ToList();
        Replace(PageNumbers,Enumerable.Range(Math.Max(1,page-2),Math.Min(5,knownPages-Math.Max(1,page-2)+1)));
        FilterPosts();
        if(demo) {foreach(var card in currentCards)card.ImagePath=$"preview{(card.Post.Id%6)+1}.jpg";}
        else _=LoadThumbnailsAsync(currentCards.ToArray(),ct);
    }
    async Task LoadThumbnailsAsync(CardViewModel[] cards,CancellationToken ct)
    {
        if(!PrefetchThumbnails)return;
        await Task.WhenAll(cards.Select(async card=>{
            try {if(card.Post.Media.FirstOrDefault() is {} media && service!=null){var path=await service.GetMediaAsync(media,false,ct);if(navigation.IsCurrent(ct))card.ImagePath=path;}}
            catch(Exception) { /* Missing media retains the explicit placeholder. */ }
        }));
    }
    public Task PreviousAsync()=>ChangePageAsync(page-1);
    public Task NextAsync()=>ChangePageAsync(page+1);
    public async Task RefreshAsync()
    {
        if(busy)return;
        if(IsGroupPicker) {var ct=navigation.Next();busy=true;canceled=false;error="";AllChanged();try{if(!demo)await LoadGroupsAsync(ct);}catch(OperationCanceledException){}catch(Exception e){if(navigation.IsCurrent(ct))error=ExplainShell(e);}finally{if(navigation.IsCurrent(ct)){busy=false;AllChanged();}}}
        else if(SelectedTopic!=null)await SelectTopicAsync(SelectedTopic,true);
        else if(SelectedGroup!=null)await SelectGroupAsync(SelectedGroup);
    }
    public void Cancel()
    {
        if(loggingOut)return;navigation.Next();busy=false;
        if(refreshFallback!=null){pager=refreshFallback;refreshFallback=null;historyLoaded=true;cached=true;}
        canceled=IsGroupPicker?Groups.Count==0:IsOverview?Topics.Count==0:Cards.Count==0;status="ยกเลิกการโหลดแล้ว";AllChanged();
    }
    public void BackToGroups(){if(loggingOut)return;navigation.Next();rememberedPagers.Trim();refreshFallback=null;busy=false;showGroups=true;canceled=!groupsLoaded;error="";foreach(var group in RailGroups)group.IsSelected=false;AllChanged();}
    public async Task PreviewAsync()
    {
        CancelAuthentication();demo=true;connected=true;showGroups=true;
        groupsLoaded=true;
        rememberedPagers.Clear();rememberedPages.Clear();postQueries.Clear();topicQueries.Clear();canceled=false;
        allGroups=[new(1,"Creative Exchange",true),new(2,"Design Library",true),new(3,"Weekend Explorers",true),new(4,"Developer Notes",true)];
        Replace(RailGroups,allGroups);FilterGroups();AllChanged();await Task.CompletedTask;
    }
    internal void SetVerificationGroups(IEnumerable<GroupInfo> groups)
    {
        if(!demo)throw new InvalidOperationException("Verification requires isolated preview mode.");
        groupsLoaded=true;allGroups=groups.ToList();groupQuery="";canceled=false;Replace(RailGroups,allGroups);FilterGroups();BackToGroups();
    }
    internal void SetVerificationTopics(IEnumerable<TopicInfo> topics)
    {
        if(!demo)throw new InvalidOperationException("Verification requires isolated preview mode.");
        topicsLoaded=true;allTopics=topics.ToList();topicQuery="";FilterTopics();BackToOverview();
    }
    public async Task LogoutAsync()
    {
        if(loggingOut)return;loggingOut=true;
        navigation.Next();busy=true;AllChanged();var result=new LogoutResult(true,true);
        try {if(service!=null)result=await service.LogoutAsync();}
        catch(Exception){result=new(false,false);}
        finally
        {
            service=null;connected=false;demo=false;busy=false;loggingOut=false;qrMode=true;loginStep="phone_number";LoginInput="";
            rememberedPagers.Clear();rememberedPages.Clear();postQueries.Clear();topicQueries.Clear();refreshFallback=null;
            groupsLoaded=false;topicsLoaded=false;
            allGroups=[];allTopics=[];pager=null;currentCards=[];Cards.Clear();Groups.Clear();Topics.Clear();RailGroups.Clear();SelectedGroup=null;SelectedTopic=null;
            status="Your groups. A clearer view.";error=!result.LocalCleared?"Could not fully clear local account data. Close the app and remove its account.session and accounts folder under %LOCALAPPDATA%/TeleGramBlog. Revoke this session in Telegram Settings → Devices.":result.RemoteRevoked?"":"Local sign-out completed. Remote revocation could not be confirmed; check Telegram Settings → Devices.";AllChanged();
        }
    }
    public void RestartLogin()
    {
        CancelAuthentication();new ProtectedSessionStore(Path.Combine(root,"account.session")).Clear();
        loginStep="phone_number";LoginInput="";error="";busy=false;AllChanged();
    }
    public void Dispose(){avatarsLifetime.Cancel();avatarsLifetime.Dispose();authGeneration++;qrLogin.Stop();navigation.Dispose();service?.Dispose();}
}

internal sealed class AppNotConfiguredException:Exception
{
    public AppNotConfiguredException():base("This copy of TeleGramBlog has not been configured for Telegram yet. The app owner needs to finish application setup before sign-in is available. See the setup guide included with the app."){}
}

internal sealed class DemoHistorySource:IHistorySource
{
    public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken cancellationToken)
    {
        string[] titles=["A study in blue","Spaces that breathe","Small details, better interfaces","The editorial collection","Objects with purpose","Coastal notes"];
        var posts=Enumerable.Range(1,54).Reverse().Select(i=>new HistoryMessage(i,0,DateTime.Today.AddDays(i-55),
            titles[(i-1)%6]+"\nA little inspiration, saved for another day.",["https://example.com/"],new MediaRef(1,i,i==54?"video":"photo"))).ToArray();
        var mixed=posts.SelectMany(p=>p.Id==53
            ?new[]{p with {Id=530,AlbumId=99,Media=new MediaRef(1,530,"photo")},p with {Id=531,AlbumId=99,Media=new MediaRef(1,531,"video")},p with {Id=532,AlbumId=99,Media=new MediaRef(1,532,"photo")}}
            :new[]{p}).ToArray();
        return Task.FromResult(new HistoryBatch(mixed,1,true));
    }
}
