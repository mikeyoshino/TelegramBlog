using TeleGramBlog.Telegram;
namespace TeleGramBlog.Tests;

public class MobileSessionTests
{
    [Fact]
    public void TelegramServiceUsesInjectedSessionWithoutWindowsProtection()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var session = new Session();
        using var service = new TelegramService(new(12345, new string('a', 32)), root, session, "Luma test");
        Assert.True(session.Loaded);
        Assert.False(service.HasSession);
        Assert.False(File.Exists(Path.Combine(root, "account.session")));
    }

    [Fact]
    public void FailedProtectedSessionCleanupStillRemovesAccountDataAndReportsFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var store = new LocalStore(root);
        Directory.CreateDirectory(store.AccountPath(123));
        File.WriteAllText(Path.Combine(store.AccountPath(123), "favorites.json"), "private metadata");
        try
        {
            Assert.False(store.ClearLocalData(new Session { FailClear = true }));
            Assert.False(Directory.Exists(store.AccountPath(123)));
        }
        finally { Directory.Delete(root, true); }
    }

    sealed class Session : ISessionStore
    {
        public bool Loaded { get; private set; }
        public bool FailClear { get; init; }
        public byte[]? Load() { Loaded = true; return null; }
        public void Save(byte[] bytes) { }
        public void Clear() { if (FailClear) throw new IOException("Keychain unavailable"); }
    }
}
