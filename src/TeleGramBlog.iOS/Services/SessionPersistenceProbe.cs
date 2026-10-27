#if DEBUG
using System.Text.Json;
namespace Luma.Services;

// Separate synthetic Keychain item. Never reads, overwrites or deletes the user's Telegram session.
internal static class SessionPersistenceProbe
{
    public static void Run(bool write)
    {
        var store = new KeychainSessionStore("app.telegramblog.luma.persistence-check", "synthetic");
        byte[] expected = [76, 85, 77, 65, 1, 2, 3, 4];
        var path = Path.Combine(FileSystem.CacheDirectory, "session-persistence-probe.json");
        try
        {
            if(write)
            {
                store.Save([0]);
                store.Save(expected); // Exercise both initial creation and session updates.
            }
            var restored = store.Load();
            var passed = restored != null && restored.SequenceEqual(expected);
            File.WriteAllText(path, JsonSerializer.Serialize(new { stage = write ? "write" : "read-after-relaunch", passed }));
            if(!write && passed) store.Clear();
        }
        catch(Exception error)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { stage = write ? "write" : "read-after-relaunch", passed = false, error = error.Message }));
        }
    }
}
#endif
