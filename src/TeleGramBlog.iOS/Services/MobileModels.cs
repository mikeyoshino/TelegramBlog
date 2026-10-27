namespace Luma.Services;
public sealed record MobileMedia(string Id, string Kind);
public sealed record MobilePost(string Id, string Group, string Topic, string GroupTitle, string TopicTitle,
    string Title, DateTime Date, string? Source, string[] Links, MobileMedia[] Media, string Text = "");
public sealed record MobileGroup(string Id, string Title, bool IsForum, int? UnreadCount, DateTime? LastMessageAt);
public sealed record MobileTopic(string Id, string Title);
