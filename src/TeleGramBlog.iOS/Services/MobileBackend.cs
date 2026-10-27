using System.Text.Json;
using TeleGramBlog.Core;
using TeleGramBlog.App.ViewModels;
using TeleGramBlog.Telegram;
namespace Luma.Services;

public sealed class MobileBackend : IDisposable
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly string root = RuntimePlatform.DataRoot;
    readonly ISessionStore sessions = RuntimePlatform.CreateSessionStore();
    readonly SemaphoreSlim authGate = new(1);
    readonly SemaphoreSlim favoritesGate = new(1);
    SqliteFavoritesStore? savedStore;
    readonly Dictionary<string, MobilePost> known = [];
    Dictionary<string, MobilePost> favorites = [];
    IReadOnlyList<GroupInfo> groups = [];
    IReadOnlyList<TopicInfo> topics = [];
    TelegramService? telegram;
    CancellationTokenSource navigation = new();
    CancellationTokenSource media = new();
    MainViewModel desktop;
    readonly QrLoginFlow qrLogin = new();
    int? historyVersion;
    public event Action<string, object>? Changed;
    public MobileBackend()
    {
        desktop = NewDesktop();
        qrLogin.Changed += () => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke("qr", new { image = qrLogin.Url is {} url ? "data:image/png;base64," + Convert.ToBase64String(LoginQrImage.Encode(url)) : null, password = qrLogin.NeedsPassword, running = qrLogin.IsRunning }));
    }
    MainViewModel NewDesktop()
    {
        var vm = new MainViewModel(dispatch: MainThread.BeginInvokeOnMainThread, dataRoot: root) { MediaOnly = true, PrefetchThumbnails = false };
        vm.PropertyChanged += (_, _) => { if(historyVersion is {} version && vm == desktop && vm.SelectedTopic != null) Changed?.Invoke("history", HistorySnapshot(version)); };
        return vm;
    }
    int generation;
    int authVersion;
    bool disposed;
    readonly NativeMedia player = new();
#if WINDOWS
    public Func<Microsoft.UI.Xaml.FrameworkElement?>? MediaHost { get; set; }
#else
    public Func<UIKit.UIView?>? MediaHost { get; set; }
#endif
    public bool IsAuthenticated => telegram?.AccountId is > 0;
    TelegramService Service => telegram ?? throw new InvalidOperationException("กรุณาเชื่อมต่อ Telegram ก่อน");
    SqliteFavoritesStore SavedStore => savedStore ?? throw new InvalidOperationException("กรุณาเชื่อมต่อบัญชีก่อน");
    public async Task<object?> DispatchAsync(string action, JsonElement data)
    {
        if (disposed) throw new ObjectDisposedException(nameof(MobileBackend));
        return action switch
        {
            "start" => await StartAsync(),
            "resume" => await ForegroundAsync(),
            "login" => await LoginAsync(data.GetProperty("input").GetString() ?? ""),
            "logout" => await LogoutAsync(),
            "qrStart" => await QrAsync(),
            "qrPassword" => SubmitQrPassword(data),
            "authCancel" => CancelAuthentication(),
            "search" => Search(data),
            "avatar" => await AvatarAsync(data),
            "backGroups" => BackGroups(),
            "groups" => await GroupsAsync(data),
            "groupUnread" => await GroupUnreadAsync(),
            "topics" => await TopicsAsync(data.GetProperty("group").GetString()!),
            "history" => await HistoryAsync(data),
            "thumbnail" => await ThumbnailAsync(data),
            "original" => await OriginalAsync(data),
            "organization" => await OrganizationAsync(data),
            "favorite" => await SaveFavoritesAsync(data),
            "share" => await ShareAsync(data),
            "play" => await PlayAsync(data),
            "videoLayout" => UpdateVideoLayout(data),
            "expandVideo" => ExpandVideo(),
            "source" => await SourceAsync(data),
            "cancel" => Cancel(),
            "closeMedia" => CloseMedia(),
            _ => throw new ArgumentException("Unknown app action.")
        };
    }
    async Task EnsureServiceAsync(int version)
    {
        if (Preferences.Default.Get("luma.logoutPending", false)) throw new InvalidOperationException("กรุณาล้างข้อมูลบัญชีเดิมให้เสร็จก่อนเชื่อมต่อใหม่");
        if(version != authVersion) throw new OperationCanceledException();
        if (telegram != null) return;
        var config = await RuntimePlatform.ReadConfigAsync();
        if (config is not { IsValid: true }) throw new InvalidOperationException("การตั้งค่า Telegram API ของแอปไม่ถูกต้อง");
        if(version != authVersion) throw new OperationCanceledException();
        telegram ??= new TelegramService(config, root, sessions, RuntimePlatform.DeviceName);
    }
    async Task<object> StartAsync()
    {
#if DEBUG && IOS
        if (Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-inline-player"))
        {
            await InlinePlaybackProbe.RunAsync(MediaHost?.Invoke() ?? throw new InvalidOperationException("Video view is not ready."));
            return new { preview = true };
        }
        if (Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-favorites-write") || Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-favorites-read"))
        {
            FavoritesPersistenceProbe.Run(Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-favorites-write"));
            return new { connected = false, configured = true };
        }
        if (Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-session-write") || Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-session-read"))
        {
            SessionPersistenceProbe.Run(Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-session-write"));
            return new { connected = false, configured = true };
        }
        if (Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains("--verify-streaming"))
        { await StreamingProbe.RunAsync(); return new { preview = true }; }
#endif
        if (Preferences.Default.Get("luma.logoutPending", false)) return new { logoutPending = true };
        if (RuntimePlatform.HasArgument("--design-preview")) return new { preview = true };
        if (await RuntimePlatform.ReadConfigAsync() is not { IsValid: true }) return new { connected = false, configured = false };
        return await ResumeAsync();
    }
    async Task<object> ResumeAsync()
    {
        var version = authVersion;
        await authGate.WaitAsync();
        TelegramService? active = null;
        try
        {
            await EnsureServiceAsync(version);
            active = Service;
            if(!active.HasSession){LifecycleDiagnostics.Record("no-saved-session", false);return new { connected = false, configured = true };}
            await active.ResumeSessionAsync().WaitAsync(TimeSpan.FromSeconds(25));
            return await ConnectedAsync(active, version);
        }
        catch when(version != authVersion || (active != null && !ReferenceEquals(active, telegram)))
        { throw new OperationCanceledException(); }
        catch(Exception error)
        {
            // Release the failed transport, retaining the protected session for an explicit retry.
            active?.Dispose();
            if(ReferenceEquals(active, telegram)) telegram = null;
            return new { connected = false, configured = true, restorePending = true,
                message = error is TL.RpcException { Code: 401 }
                    ? "Telegram ไม่ยอมรับเซสชันเดิม กรุณาตรวจสอบ Devices ใน Telegram ก่อนเชื่อมต่อใหม่"
                    : "เชื่อมต่อบัญชีเดิมยังไม่สำเร็จ เซสชันยังถูกเก็บไว้ กรุณาตรวจสอบอินเทอร์เน็ตแล้วลองอีกครั้ง" };
        }
        finally { authGate.Release(); }
    }
    async Task<object> ForegroundAsync()
    {
        var version = authVersion;
        var token = navigation.Token;
        await authGate.WaitAsync(token);
        try
        {
            if(version != authVersion)throw new OperationCanceledException();
            var active = telegram;
            if(active?.AccountId is not > 0){LifecycleDiagnostics.Record("authentication-required", false);return new { connected = false };}
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            try { await active.ReconnectSessionAsync(timeout.Token); }
            catch(OperationCanceledException) when(!token.IsCancellationRequested)
            { throw new IOException("เชื่อมต่อ Telegram ยังไม่สำเร็จ กรุณาตรวจสอบ Wi-Fi หรืออินเทอร์เน็ตมือถือ แล้วลองอีกครั้ง"); }
            if(version != authVersion || !ReferenceEquals(active, telegram))throw new OperationCanceledException();
            LifecycleDiagnostics.Record("connection-restored", true);
            return new { connected = true };
        }
        finally { authGate.Release(); }
    }
    async Task<object> LoginAsync(string input)
    {
        var version = authVersion;
        await authGate.WaitAsync();
        try
        {
            await EnsureServiceAsync(version);
            var active = Service;
            string? step;
            try { step = await active.LoginAsync(input); }
            catch when(version != authVersion || !ReferenceEquals(active, telegram))
            { throw new OperationCanceledException(); }
            catch
            {
                active.Dispose(); telegram = null;
                // A faulted PhoneLoginFlow cannot be reused. The next attempt starts fresh.
                throw new InvalidOperationException("เข้าสู่ระบบไม่สำเร็จ กรุณาเริ่มใหม่ด้วยเบอร์โทร ตรวจสอบรหัสและการเชื่อมต่อของคุณ");
            }
            if(version != authVersion || !ReferenceEquals(active, telegram)) throw new OperationCanceledException();
            if (step != null) return new { connected = false, configured = true, step };
            return await ConnectedAsync(active, version);
        }
        finally { authGate.Release(); }
    }
    async Task<object> ConnectedAsync(TelegramService active, int version)
    {
        if(version != authVersion || !ReferenceEquals(active, telegram)) throw new OperationCanceledException();
        LifecycleDiagnostics.Record("authenticated", true);
        savedStore = new SqliteFavoritesStore(active.Store.AccountPath(active.AccountId));
        favorites = SavedStore.Load();
        foreach (var p in favorites.Values) known[p.Id] = p;
        await desktop.UseConnectedServiceAsync(active,loadGroups:false);
        if(version != authVersion || !ReferenceEquals(active, telegram)) throw new OperationCanceledException();
        return new { connected = true, account = Service.AccountId.ToString(), name = Service.AccountName,
            favorites = favorites.Values.ToArray() };
    }
    async Task<object> QrAsync()
    {
        if (qrLogin.IsRunning) throw new InvalidOperationException("QR login is already active.");
        var version = authVersion;
        await EnsureServiceAsync(version);
        var active = Service;
        await qrLogin.RunAsync(active.LoginWithQrAsync);
        return await ConnectedAsync(active, version);
    }
    object? SubmitQrPassword(JsonElement data) { qrLogin.SubmitPassword(data.GetProperty("input").GetString() ?? ""); return null; }
    object? CancelAuthentication()
    {
        authVersion++; qrLogin.Stop();
        if (telegram?.AccountId is not > 0) { telegram?.Dispose(); telegram = null; }
        return null;
    }
    async Task<object> GroupsAsync(JsonElement data)
    {
        var active = Service;
        var version = authVersion;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(navigation.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        LifecycleDiagnostics.Record("groups-loading",true);
        try
        {
            var updated = await active.GetGroupsAsync(timeout.Token,includeForumUnread:false);
            if(version != authVersion || !ReferenceEquals(active, telegram)) throw new OperationCanceledException();
            groups = updated.ToArray();
            LifecycleDiagnostics.Record("groups-ready",true);
            return groups.OrderByDescending(g => g.LastMessageAt).ThenBy(g => g.Title)
                .Select(g => new MobileGroup(g.Id.ToString(), g.Title, g.IsForum, g.UnreadCount, g.LastMessageAt)).ToArray();
        }
        catch { LifecycleDiagnostics.Record("groups-failed",IsAuthenticated); throw; }
    }
    async Task<object> GroupUnreadAsync()
    {
        var active = Service;
        var version = authVersion;
        var counts = await active.GetForumUnreadCountsAsync(groups.Where(g=>g.IsForum).Select(g=>g.Id).ToArray(),navigation.Token);
        if(version != authVersion || !ReferenceEquals(active,telegram))throw new OperationCanceledException();
        LifecycleDiagnostics.Record(counts.Values.Any(n=>n==null)?"unread-partial":"unread-ready",true);
        return counts.Select(p=>new { id=p.Key.ToString(),unreadCount=p.Value }).ToArray();
    }
    GroupInfo Group(string id) => groups.FirstOrDefault(g => g.Id.ToString() == id)
        ?? throw new InvalidOperationException("กรุณาเลือกกลุ่มใหม่อีกครั้ง");
    async Task<object> TopicsAsync(string id)
    {
        historyVersion = null;
        await desktop.SelectGroupAsync(Group(id));
        if(desktop.HasError) throw new IOException(desktop.Error);
        topics = desktop.Topics.ToArray();
        return topics.Select(t => new MobileTopic(t.Id.ToString(), t.Title)).ToArray();
    }
    object? BackGroups() { historyVersion = null; desktop.BackToGroups(); return null; }
    async Task<object?> AvatarAsync(JsonElement data)
    {
        var path = await Service.GetGroupAvatarAsync(Group(data.GetProperty("group").GetString()!).Id, navigation.Token);
        return path == null ? null : "data:image/jpeg;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(path, navigation.Token));
    }
    object HistorySnapshot(int version)
    {
        var group = desktop.SelectedGroup; var topic = desktop.SelectedTopic;
        var cards = group == null || topic == null ? [] : desktop.Cards.Select(c => new MobilePost($"{group.Id}:{c.Post.Id}", group.Id.ToString(), topic.Id.ToString(),
            group.Title, topic.Title, c.Post.Title, c.Post.Date, Service.PostUrl(group, c.Post.Id), c.Post.Links.ToArray(),
            c.Post.Media.Select(m => new MobileMedia($"{m.ChatId}:{m.MessageId}", m.Kind)).ToArray(), c.Post.Text)).ToArray();
        known.Clear();
        foreach(var p in favorites.Values.Concat(cards)) known[p.Id] = p;
        return new { version, posts = cards, page = desktop.CurrentPage, knownPages = desktop.KnownPages,
            hasNext = desktop.HasNextPage, pageSize = desktop.PageSize, loadedCount = desktop.LoadedCount,
            query = desktop.PostQuery, cached = desktop.IsCached, fetchedAt = desktop.HistoryFetchedAt,
            busy = desktop.IsBusy, error = desktop.Error, canceled = desktop.ShowCanceled };
    }
    async Task<object> HistoryAsync(JsonElement data)
    {
        var group = Group(data.GetProperty("group").GetString()!);
        if(desktop.SelectedGroup?.Id != group.Id) throw new InvalidOperationException("กรุณาเลือกกลุ่มใหม่");
        var topicId = data.GetProperty("topic").GetString();
        var topic = topics.FirstOrDefault(t => t.Id.ToString() == topicId);
        if(topic == null) throw new InvalidOperationException("กรุณาเลือกหัวข้อก่อนดูโพสต์");
        var version = data.GetProperty("version").GetInt32();
        var requested = data.GetProperty("page").GetInt32();
        if(requested < 0 || requested > 10000) throw new ArgumentException("Invalid page.");
        var size = data.TryGetProperty("pageSize", out var sizeValue) ? sizeValue.GetInt32() : 24;
        if(size is not (24 or 48)) throw new ArgumentException("Invalid page size.");
        var resized = desktop.PageSize != size;
        historyVersion = version;
        try
        {
            desktop.PageSize = size;
            if(data.TryGetProperty("refresh", out var refresh) && refresh.GetBoolean())
                await desktop.SelectTopicAsync(topic, true);
            else if(desktop.SelectedTopic?.Id != topic.Id || requested == 0 || resized)
                await desktop.SelectTopicAsync(topic, true);
            else await desktop.ChangePageAsync(requested);
            return HistorySnapshot(version);
        }
        finally { if(historyVersion == version) historyVersion = null; }
    }
    object Search(JsonElement data)
    {
        desktop.PostQuery = data.GetProperty("query").GetString() ?? "";
        return HistorySnapshot(data.GetProperty("version").GetInt32());
    }
    MobilePost Post(JsonElement data) => known.GetValueOrDefault(data.GetProperty("post").GetString()!)
        ?? throw new InvalidOperationException("รายการนี้ไม่ได้อยู่ในหน้าปัจจุบัน กรุณาเปิดกลุ่มอีกครั้ง");
    (MobilePost Post, MobileMedia Media, MediaRef Reference) Resolve(JsonElement data)
    {
        var post = Post(data); var index = data.GetProperty("index").GetInt32();
        if (index < 0 || index >= post.Media.Length) throw new ArgumentException("Invalid media.");
        var item = post.Media[index]; var parts = item.Id.Split(':');
        return (post, item, new(long.Parse(parts[0]), int.Parse(parts[1]), item.Kind));
    }
    Task<object?> ThumbnailAsync(JsonElement data) => ReadImageAsync(data, false);
    Task<object?> OriginalAsync(JsonElement data) => ReadImageAsync(data, true);
    async Task<object?> ReadImageAsync(JsonElement data, bool original)
    {
        var item = Resolve(data);
        if(original && item.Media.Kind is not ("image" or "photo")) return null;
        var store = SavedStore;
        var bytes = favorites.ContainsKey(item.Post.Id) ? store.ReadMedia(item.Post.Id, item.Media.Id, original) : null;
        if(bytes != null) return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
        var version = authVersion;
        var token = original ? media.Token : navigation.Token;
        var path = await Service.GetMediaAsync(item.Reference, original, token);
        if(path == null) return null;
        bytes = await File.ReadAllBytesAsync(path, token);
        if(version != authVersion || !ReferenceEquals(store, savedStore)) throw new OperationCanceledException();
        // Old JSON favorites acquire their local images when they are opened.
        if(favorites.ContainsKey(item.Post.Id)) store.PutMediaIfSaved(item.Post.Id, item.Media.Id, original, bytes);
        return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
    }
    async Task<object> OrganizationAsync(JsonElement data)
    {
        var version = authVersion;
        await favoritesGate.WaitAsync();
        try
        {
            if(version != authVersion) throw new OperationCanceledException();
            var store = SavedStore;
            var operation = data.TryGetProperty("operation",out var op) ? op.GetString() : "load";
            switch(operation)
            {
                case "load": break;
                case "collection":
                    store.SaveCollection(data.TryGetProperty("id",out var id) ? id.GetString() : null,data.GetProperty("name").GetString() ?? ""); break;
                case "delete": store.DeleteCollection(data.GetProperty("id").GetString()!); break;
                case "item":
                    store.SaveOrganization(data.GetProperty("post").GetString()!,data.GetProperty("collectionId").GetString(),
                        data.GetProperty("note").GetString() ?? "",data.GetProperty("viewed").GetBoolean()); break;
                default: throw new ArgumentException("Unknown organization operation.");
            }
            return store.LoadOrganization();
        }
        finally { favoritesGate.Release(); }
    }
    async Task<object> SaveFavoritesAsync(JsonElement data)
    {
        var version = authVersion;
        var token = navigation.Token;
        await favoritesGate.WaitAsync(token);
        string? staging = null;
        try
        {
            if(version != authVersion) throw new OperationCanceledException();
            var active = Service;
            var store = SavedStore;
            var next = new Dictionary<string, MobilePost>(favorites);
            var added = new HashSet<string>();
            foreach(var entry in data.GetProperty("changes").EnumerateArray())
            {
                var id = entry.GetProperty("id").GetString()!;
                if(entry.GetProperty("saved").GetBoolean())
                {
                    if(!known.TryGetValue(id, out var post)) throw new InvalidOperationException("ไม่พบโพสต์นี้ กรุณาโหลดใหม่");
                    next[id] = post; added.Add(id);
                }
                else { next.Remove(id); added.Remove(id); }
            }
            if(next.Count > 500) throw new InvalidOperationException("เก็บได้สูงสุด 500 รายการ กรุณานำบางรายการออกก่อน");
            var files = new List<FavoriteMediaFile>();
            foreach(var id in added)
            foreach(var item in next[id].Media)
            foreach(var original in item.Kind is "image" or "photo" ? new[] {false, true} : new[] {false})
            {
                if(store.HasMedia(id, item.Id, original)) continue;
                var parts = item.Id.Split(':');
                var reference = new MediaRef(long.Parse(parts[0]), int.Parse(parts[1]), item.Kind);
                var path = await active.GetMediaAsync(reference, original, token);
                if(path == null)
                {
                    if(original) throw new IOException("ยังเก็บภาพต้นฉบับไม่ได้ กรุณาลองอีกครั้ง");
                    continue;
                }
                token.ThrowIfCancellationRequested();
                staging ??= Path.Combine(active.Store.AccountPath(active.AccountId), "favorite-staging", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                var target = Path.Combine(staging, Guid.NewGuid().ToString("N"));
                File.Copy(path, target);
                files.Add(new(id, item.Id, original, target));
            }
            token.ThrowIfCancellationRequested();
            if(version != authVersion || !ReferenceEquals(active, telegram)) throw new OperationCanceledException();
            // Import one image at a time; never keep a whole album's bytes in memory.
            if(data.TryGetProperty("collectionId",out var collectionId)) store.Apply(next, files, added, collectionId.GetString());
            else store.Apply(next, files);
            favorites = next;
            return favorites.Values.ToArray();
        }
        finally
        {
            try { if(staging != null && Directory.Exists(staging)) Directory.Delete(staging, true); }
            finally { favoritesGate.Release(); }
        }
    }
    async Task<object?> PlayAsync(JsonElement data)
    {
        var item = Resolve(data); var token = media.Token;
        var source = await Service.OpenVideoAsync(item.Reference, token);
        try { token.ThrowIfCancellationRequested(); player.PlayStream(source, MediaHost?.Invoke() ?? throw new InvalidOperationException("Video view is not ready."), ReadVideoFrame(data)); }
        catch { source.Dispose(); throw; }
        return null;
    }
    static VideoFrame ReadVideoFrame(JsonElement data) => JsonSerializer.Deserialize<VideoFrame>(data.GetProperty("frame"), Json)
        ?? throw new ArgumentException("Missing video frame.");
    object? UpdateVideoLayout(JsonElement data) { player.UpdateFrame(ReadVideoFrame(data)); return null; }
    object? ExpandVideo() { player.Expand(); return null; }
    async Task<object?> ShareAsync(JsonElement data)
    {
        var item = Resolve(data); var token = media.Token;
        TemporaryMediaFile? temporary = null;
        string? savedImage = null;
        try
        {
            var stored = item.Media.Kind == "video" ? null : SavedStore.ReadMedia(item.Post.Id, item.Media.Id, true);
            if(stored != null)
            {
                savedImage = Path.Combine(FileSystem.CacheDirectory, $"saved-image-{Guid.NewGuid():N}.jpg");
                await File.WriteAllBytesAsync(savedImage, stored, token);
            }
            var path = savedImage ?? (item.Media.Kind == "video"
                ? (temporary = await Service.GetVideoAsync(item.Reference, null, token)).Path
                : await Service.GetMediaAsync(item.Reference, true, token));
            token.ThrowIfCancellationRequested();
            if (path == null) throw new IOException("ไม่สามารถโหลดไฟล์นี้ได้");
#if WINDOWS
            await DesktopFileExport.SaveAsync(path, item.Post.Title);
#else
            await Share.Default.RequestAsync(new ShareFileRequest { Title = item.Post.Title, File = new ShareFile(path) });
#endif
        }
        finally { temporary?.Dispose(); if(savedImage != null && File.Exists(savedImage)) File.Delete(savedImage); }
        return null;
    }
    async Task<object?> SourceAsync(JsonElement data)
    {
        var post = Post(data);
        if(data.TryGetProperty("linkIndex", out var linkIndex))
        {
            var index = linkIndex.GetInt32();
            if(index < 0 || index >= post.Links.Length || !Uri.TryCreate(post.Links[index], UriKind.Absolute, out var link) || link.Scheme is not ("https" or "http")) throw new ArgumentException("Invalid link.");
            await Launcher.Default.OpenAsync(link); return null;
        }
        var url = post.Source ?? post.Links.FirstOrDefault(l => Uri.TryCreate(l, UriKind.Absolute, out var u) && u.Scheme == "https");
        if (url == null) throw new InvalidOperationException("Telegram ไม่ได้ให้ลิงก์สำหรับกลุ่มนี้");
        await Launcher.Default.OpenAsync(new Uri(url)); return null;
    }
    async Task<object> LogoutAsync()
    {
        authVersion++; qrLogin.Stop(); Cancel(); CloseMedia();
        Preferences.Default.Set("luma.logoutPending", true);
        await favoritesGate.WaitAsync();
        try
        {
        if (telegram == null)
        {
            var cleared = new LocalStore(root).ClearLocalData(sessions);
            if (cleared) Preferences.Default.Remove("luma.logoutPending");
            return new { remoteRevoked = false, localCleared = cleared };
        }
        var result = await telegram.LogoutAsync();
        if (result.LocalCleared) Preferences.Default.Remove("luma.logoutPending");
        historyVersion = null; desktop.Dispose(); desktop = NewDesktop();
        telegram = null; known.Clear(); favorites.Clear(); groups = []; topics = [];
        savedStore = null;
        return result;
        }
        finally { favoritesGate.Release(); }
    }
    object? Cancel()
    {
        generation++; historyVersion = null; desktop.Cancel();
        navigation.Cancel(); navigation.Dispose(); navigation = new();
        return null;
    }
    object? CloseMedia()
    {
        media.Cancel(); media.Dispose(); media = new(); player.Close(); return null;
    }
    public void Suspend() { LifecycleDiagnostics.Record("background", IsAuthenticated); Cancel(); CloseMedia(); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; authVersion++; Suspend(); qrLogin.Stop(); desktop.Dispose(); telegram?.Dispose(); navigation.Dispose(); media.Dispose();
    }
}
