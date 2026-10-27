namespace TeleGramBlog.Telegram;

/// <summary>Platform-specific protection for the Telegram transport session.</summary>
public interface ISessionStore
{
    byte[]? Load();
    void Save(byte[] bytes);
    void Clear();
}
