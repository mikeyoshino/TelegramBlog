namespace TeleGramBlog.Core;

public record GroupInfo(long Id, string Title, bool IsForum, string? Username = null):System.ComponentModel.INotifyPropertyChanged
{
    public int? UnreadCount { get; init; }
    public DateTime? LastMessageAt { get; init; }
    string? avatarPath;
    int? knownTopicCount;
    public int? KnownTopicCount {get=>knownTopicCount;set{knownTopicCount=value;PropertyChanged?.Invoke(this,new(nameof(GroupMeta)));}}
    public string GroupMeta=>KnownTopicCount is {} count&&IsForum?$"มีหัวข้อ · {count} หัวข้อ":Kind;
    bool selected;
    public bool IsSelected {get=>selected;set{selected=value;PropertyChanged?.Invoke(this,new(nameof(IsSelected)));PropertyChanged?.Invoke(this,new(nameof(SelectionColor)));}}
    public string SelectionColor=>IsSelected?"#087DFA":"Transparent";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string? AvatarPath {get=>avatarPath;set{avatarPath=value;PropertyChanged?.Invoke(this,new(nameof(AvatarPath)));PropertyChanged?.Invoke(this,new(nameof(HasAvatar)));}}
    public bool HasAvatar=>AvatarPath!=null;
    public string AvatarColor=>new[]{"#DFEBFA","#E8E3F5","#DDEFEA","#F3E8DC","#E4E9F0"}[(int)((ulong)Id%5)];
    public string Initial => string.IsNullOrEmpty(Title) ? "G" : System.Globalization.StringInfo.GetNextTextElement(Title);
    public string Kind => IsForum ? "มีหัวข้อ" : "โพสต์รวม";
}
public record TopicInfo(int Id, string Title,int IconColor=0)
{
    public string IconTint=>IconColor==0?"#5684B1":$"#{IconColor&0xFFFFFF:X6}";
}
public record MediaRef(long ChatId, int MessageId, string Kind);
public record HistoryMessage(int Id, long AlbumId, DateTime Date, string Text, IReadOnlyList<string> Links, MediaRef? Media = null);
public record HistoryBatch(IReadOnlyList<HistoryMessage> Messages, int NextBeforeId, bool EndReached);
public record PostCard(int Id, DateTime Date, string Text, IReadOnlyList<string> Links, IReadOnlyList<MediaRef> Media)
{
    public string Title => string.IsNullOrWhiteSpace(Text) ? (Links.Count > 0 ? new Uri(Links[0]).Host : "Photo collection") : Text.Split('\n')[0];
    public string Domain => Links.Count > 0 ? new Uri(Links[0]).Host : (Media.Count > 1 ? $"{Media.Count} media" : "Telegram media");
    public string DateLabel => Date.ToLocalTime().ToString("dd MMM yyyy");
}
public record PostPage(IReadOnlyList<PostCard> Cards, int Page, int KnownPages, bool HasNext, bool EndReached, int LoadedCount);
public record HistorySnapshot(List<HistoryMessage> Messages, int BeforeId, bool EndReached,DateTimeOffset? FetchedAt=null);
public interface IHistorySource { Task<HistoryBatch> FetchAsync(int beforeId, CancellationToken cancellationToken); }
