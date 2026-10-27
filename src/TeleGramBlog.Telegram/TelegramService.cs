using TeleGramBlog.Core;
using TL;
using WTelegram;
namespace TeleGramBlog.Telegram;

public sealed record TelegramConfig(int ApiId,string ApiHash)
{
    public bool IsValid=>ApiId>0 && ApiHash is {Length:32} && ApiHash.All(Uri.IsHexDigit);
}
public sealed record LogoutResult(bool RemoteRevoked,bool LocalCleared);

public sealed class TelegramService : IDisposable
{
    readonly Client client;
    readonly ProtectedSessionStore session;
    readonly Dictionary<long,ChatBase> chats=[];
    readonly SemaphoreSlim mediaGate=new(3);
    readonly TelegramRequestGate requests=new();
    readonly CancellationTokenSource lifetime=new();
    readonly object logoutGate=new();
    Task<LogoutResult>? logoutTask;
    Func<Task<string>>? qrPassword;
    PhoneLoginFlow? phoneLogin;
    string phoneNumber="";
    public LocalStore Store {get;}
    public long AccountId=>client.User?.id ?? 0;
    public string AccountName=>client.User?.first_name ?? "Telegram";
    public bool HasSession=>client.UserId!=0;
    public TelegramService(TelegramConfig config,string root)
    {
        if(!config.IsValid) throw new ArgumentException("Enter a valid Telegram API ID and 32-character API hash.");
        Store=new(root); session=new(Path.Combine(root,"account.session"));
        Helpers.Log=(_,_)=>{};
        var saved=session.Load();
        client=new Client(key=>key switch {
            "api_id"=>config.ApiId.ToString(), "api_hash"=>config.ApiHash,
            "device_model"=>"TeleGramBlog Desktop", "app_version"=>"1.1", "user_id"=>"-1",
            "phone_number"=>phoneNumber,
            "password" when qrPassword!=null=>qrPassword().GetAwaiter().GetResult(),
            "verification_code" or "password" or "email" or "email_verification_code" when phoneLogin!=null=>phoneLogin.RequestInputAsync(key).GetAwaiter().GetResult(),
            "first_name" or "last_name"=>throw new InvalidOperationException("Finish account registration in the official Telegram app, then sign in again."),
            _=>null
        },saved,session.Save) {FloodRetryThreshold=0,MaxAutoReconnects=2};
    }
    public Task<string?> LoginAsync(string input)
    {
        if(phoneLogin==null)
        {
            phoneNumber=input;
            phoneLogin=new(()=>Task.Run(async()=>{await client.LoginUserIfNeeded(reloginOnFailedResume:false);}),lifetime.Token);
        }
        return phoneLogin.AdvanceAsync(input);
    }
    public async Task LoginWithQrAsync(Action<string> showQr,Func<CancellationToken,Task<string>> requestPassword,CancellationToken ct)
    {
        qrPassword=()=>requestPassword(ct);
        try{await client.LoginWithQRCode(showQr,logoutFirst:false,ct:ct);ct.ThrowIfCancellationRequested();}
        finally{qrPassword=null;}
    }
    public Task<IReadOnlyList<GroupInfo>> GetGroupsAsync(CancellationToken ct)=>requests.RunAsync(()=>GetGroupsCoreAsync(ct),ct,true);
    async Task<IReadOnlyList<GroupInfo>> GetGroupsCoreAsync(CancellationToken ct)
    {
        var all=await client.Messages_GetAllDialogs().WaitAsync(ct);
        ct.ThrowIfCancellationRequested(); chats.Clear();
        var result=new List<GroupInfo>();
        foreach(var c in all.chats.Values)
        {
            if(c is Channel channel && channel.flags.HasFlag(Channel.Flags.megagroup) && !channel.flags.HasFlag(Channel.Flags.left))
            { chats[channel.id]=channel; result.Add(new(channel.id,channel.title,channel.flags.HasFlag(Channel.Flags.forum),channel.username)); }
            else if(c is Chat group && !group.flags.HasFlag(Chat.Flags.left) && !group.flags.HasFlag(Chat.Flags.deactivated))
            { chats[-group.id]=group; result.Add(new(-group.id,group.title,false)); }
        }
        return result.OrderBy(g=>g.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    internal InputPeer Peer(long id)=>chats.TryGetValue(id,out var chat)?chat.ToInputPeer():throw new InvalidOperationException("Select the group again to reconnect.");
    public async Task<string?> GetGroupAvatarAsync(long id,CancellationToken ct)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);ct=linked.Token;
        if(!chats.TryGetValue(id,out var chat)||chat.Photo==null)return null;
        var path=Path.Combine(Store.AccountPath(AccountId),"media",$"avatar_{id}_{chat.Photo.photo_id}.jpg");
        if(File.Exists(path))return path;
        await mediaGate.WaitAsync(ct);
        try
        {
            using var stream=new MemoryStream();
            await client.DownloadProfilePhotoAsync(chat,stream,miniThumb:true);
            if(stream.Length==0)await requests.RunAsync(()=>client.DownloadProfilePhotoAsync(chat,stream),ct,true);
            ct.ThrowIfCancellationRequested();if(stream.Length==0)return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path,stream.ToArray(),ct);return path;
        }
        finally{mediaGate.Release();}
    }
    public async Task<IReadOnlyList<TopicInfo>> GetTopicsAsync(GroupInfo group,CancellationToken ct)
    {
        if(!group.IsForum) return [new(0,"All posts")];
        var topics=new Dictionary<int,TopicInfo>();
        DateTime offsetDate=default; int offsetId=0,offsetTopic=0;
        while(true)
        {
            var response=await requests.RunAsync(()=>client.Messages_GetForumTopics(Peer(group.Id),offsetDate,offsetId,offsetTopic,100).WaitAsync(ct),ct,true);
            var batch=response.topics.OfType<ForumTopic>().ToArray();
            if(batch.Length==0) break;
            foreach(var t in batch) topics[t.id]=new(t.id,t.title,t.icon_color);
            var last=batch[^1];
            if(last.id==offsetTopic) throw new InvalidOperationException("Topic list did not advance. Retry loading this group.");
            offsetId=last.top_message; offsetTopic=last.id;
            offsetDate=response.flags.HasFlag(Messages_ForumTopics.Flags.order_by_create_date)?last.date:
                response.messages.FirstOrDefault(m=>m.ID==last.top_message)?.Date ?? last.date;
            if(topics.Count>=response.count) break;
        }
        return topics.Values.OrderBy(t=>t.Id==1?0:1).ThenBy(t=>t.Title).ToArray();
    }
    public IHistorySource CreateHistorySource(GroupInfo group,TopicInfo? topic)=>new TelegramHistorySource(client,Peer(group.Id),group,topic,requests);
    public Task<string?> GetMediaAsync(MediaRef media,bool fullSize,CancellationToken ct)=>requests.RunAsync(()=>GetMediaCoreAsync(media,fullSize,ct),ct);
    async Task<string?> GetMediaCoreAsync(MediaRef media,bool fullSize,CancellationToken ct)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
        ct=linked.Token;
        await mediaGate.WaitAsync(ct);
        try
        {
            await requests.RunAsync(()=>Task.FromResult(true),ct);
            var account=AccountId; if(account==0) return null;
            var path=Path.Combine(Store.AccountPath(account),"media",$"{media.ChatId}_{media.MessageId}_{(fullSize?"full":"thumb")}.jpg");
            if(File.Exists(path)) return path;
            var peer=Peer(media.ChatId);
            var result=peer is InputPeerChannel channel
                ? await client.Channels_GetMessages(new InputChannel(channel.channel_id,channel.access_hash),[new InputMessageID {id=media.MessageId}]).WaitAsync(ct)
                : await client.Messages_GetMessages([new InputMessageID {id=media.MessageId}]).WaitAsync(ct);
            var message=result.Messages.OfType<Message>().FirstOrDefault();
            using var stream=new MemoryStream();
            if(message?.media is MessageMediaPhoto {photo:Photo photo})
            {
                var size=MediaSelection.SelectPhotoSize(photo,fullSize);
                if(size==null) return null;
                await client.DownloadFileAsync(photo,stream,size); // Keep stream alive until network write finishes.
            }
            else if(message?.media is MessageMediaDocument {document:Document doc})
            {
                var size=doc.thumbs?.OfType<PhotoSize>().OrderByDescending(s=>s.w*s.h).FirstOrDefault();
                if(fullSize && media.Kind=="image" && doc.size<=20*1024*1024) await client.DownloadFileAsync(doc,stream);
                else if(size!=null) await client.DownloadFileAsync(doc,stream,size);
                else return null;
            }
            else return null;
            ct.ThrowIfCancellationRequested();
            if(AccountId!=account) return null;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            stream.Position=0;
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                await using(var output=File.Create(temporary))await stream.CopyToAsync(output,ct);
                ct.ThrowIfCancellationRequested();File.Move(temporary,path,true);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
            Store.TrimMedia(account);return path;
        }
        finally {mediaGate.Release();}
    }
    public async Task<ChunkedMediaSource> OpenVideoAsync(MediaRef media,CancellationToken ct)
    {
        if(media.Kind!="video")throw new ArgumentException("This post is not a video.");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);ct=linked.Token;
        var doc=await GetVideoDocumentAsync(media,ct);
        var downloadClient=await client.GetClientForDC(-doc.dc_id,true).WaitAsync(ct);
        return new ChunkedMediaSource(doc.size,doc.mime_type,async(offset,count,token)=>{
            // No CDN redirect requested: authenticated origin DC supplies the bytes.
            await mediaGate.WaitAsync(token);
            try
            {
                for(var attempt=0;;attempt++)
                {
                    try
                    {
                        var location=new InputDocumentFileLocation {id=doc.id,access_hash=doc.access_hash,file_reference=doc.file_reference,thumb_size=""};
                        var response=await requests.RunAsync(()=>downloadClient.Upload_GetFile(location,offset,count).WaitAsync(token),token);
                        return response is Upload_File file?file.bytes:throw new IOException("Unsupported media response.");
                    }
                    catch(RpcException e) when(attempt==0&&e.Code==303&&e.Message=="FILE_MIGRATE_X")
                    {downloadClient=await client.GetClientForDC(-e.X,true).WaitAsync(token);}
                    catch(RpcException e) when(attempt==0&&e.Message.StartsWith("FILE_REFERENCE_"))
                    {doc=await GetVideoDocumentAsync(media,token);downloadClient=await client.GetClientForDC(-doc.dc_id,true).WaitAsync(token);}
                }
            }
            finally{mediaGate.Release();}
        },cancellationToken:lifetime.Token);
    }
    Task<Document> GetVideoDocumentAsync(MediaRef media,CancellationToken ct)=>requests.RunAsync(()=>GetVideoDocumentCoreAsync(media,ct),ct);
    async Task<Document> GetVideoDocumentCoreAsync(MediaRef media,CancellationToken ct)
    {
        if(AccountId==0)throw new InvalidOperationException("Sign in to load this video.");
        var peer=Peer(media.ChatId);
        var result=peer is InputPeerChannel channel
            ? await client.Channels_GetMessages(new InputChannel(channel.channel_id,channel.access_hash),[new InputMessageID {id=media.MessageId}]).WaitAsync(ct)
            : await client.Messages_GetMessages([new InputMessageID {id=media.MessageId}]).WaitAsync(ct);
        return result.Messages.OfType<Message>().FirstOrDefault()?.media is MessageMediaDocument {document:Document doc}
            ?doc:throw new InvalidOperationException("This video is no longer available. Open the original in Telegram.");
    }
    public async Task<TemporaryMediaFile> GetVideoAsync(MediaRef media,IProgress<double>? progress,CancellationToken ct)
    {
        if(media.Kind!="video")throw new ArgumentException("This post is not a video.");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);ct=linked.Token;
        await mediaGate.WaitAsync(ct);
        try
        {
            var account=AccountId;if(account==0)throw new InvalidOperationException("Sign in to load this video.");
            var peer=Peer(media.ChatId);
            var result=peer is InputPeerChannel channel
                ? await client.Channels_GetMessages(new InputChannel(channel.channel_id,channel.access_hash),[new InputMessageID {id=media.MessageId}]).WaitAsync(ct)
                : await client.Messages_GetMessages([new InputMessageID {id=media.MessageId}]).WaitAsync(ct);
            if(result.Messages.OfType<Message>().FirstOrDefault()?.media is not MessageMediaDocument {document:Document doc})
                throw new InvalidOperationException("This video is no longer available. Try opening the original in Telegram.");
            var extension=doc.mime_type switch {"video/webm"=>".webm","video/quicktime"=>".mov","video/x-matroska"=>".mkv",_=>".mp4"};
            var directory=Path.Combine(Store.AccountPath(account),"playback");
            var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
            if(doc.size>drive.AvailableFreeSpace-16L*1024*1024)throw new InvalidOperationException("Not enough disk space to prepare this video.");
            return await TemporaryMediaFile.DownloadAsync(directory,extension,async(stream,token)=>{
                await client.DownloadFileAsync(doc,stream,progress:(done,total)=>{
                    token.ThrowIfCancellationRequested();if(total>0)progress?.Report(Math.Clamp((double)done/total,0,1));
                });
            },ct);
        }
        finally{mediaGate.Release();}
    }
    public string? PostUrl(GroupInfo group,int messageId)=>group.Id<0?null:
        !string.IsNullOrWhiteSpace(group.Username)?$"https://t.me/{group.Username}/{messageId}":$"https://t.me/c/{group.Id}/{messageId}";
    public Task<LogoutResult> LogoutAsync()
    {lock(logoutGate)return logoutTask??=LogoutCoreAsync();}
    async Task<LogoutResult> LogoutCoreAsync()
    {
        bool remote=false,disposed=true,cleared=false;
        lifetime.Cancel();
        try { await client.Auth_LogOut().WaitAsync(TimeSpan.FromSeconds(8)); remote=true; }
        catch(Exception) { /* Local cleanup is mandatory even if remote revocation is unavailable. */ }
        finally
        {
            try{client.Dispose();}catch{disposed=false;}
            // Stop downloads before deleting the account directory; canceled downloads never publish files.
            await mediaGate.WaitAsync();await mediaGate.WaitAsync();await mediaGate.WaitAsync();
            try{cleared=Store.ClearLocalData(session);}finally{mediaGate.Release(3);}
        }
        return new(remote,cleared&&disposed);
    }
    public void Dispose() {lifetime.Cancel();client.Dispose();}
    public static string Explain(Exception e)=>e switch
    {
        RpcException r when TelegramRequestGate.IsFlood(r)=>$"Telegram asks you to wait {Math.Max(1,r.X)} seconds before trying again. Requests are paused; retry after the wait.",
        RpcException r when r.Code==401=>"Your Telegram session has expired. Disconnect and sign in again.",
        RpcException r when r.Message.Contains("PHONE_CODE")=>"The verification code is invalid or expired. Try again, or restart sign-in.",
        RpcException r when r.Message.Contains("PASSWORD_HASH")=>"The two-step verification password is incorrect. Try again.",
        RpcException r when r.Message.Contains("CHANNEL_PRIVATE")=>"This group is no longer accessible to your account.",
        _=>"Unable to complete this action. Check your connection and try again. " + (e is InvalidOperationException or ArgumentException?e.Message:"")
    };
}
