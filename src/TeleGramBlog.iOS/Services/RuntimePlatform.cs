using System.Text.Json;
using TeleGramBlog.Telegram;
namespace Luma.Services;

// Keep platform storage identity stable when changing presentation layers.
internal static class RuntimePlatform
{
#if WINDOWS
    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeleGramBlog");
    public static ISessionStore CreateSessionStore() => new ProtectedSessionStore(Path.Combine(DataRoot, "account.session"));
    public static string DeviceName => "TeleBlog for Windows";
    public static bool HasArgument(string argument) => Environment.GetCommandLineArgs().Contains(argument);
#else
    public static string DataRoot => Path.Combine(FileSystem.AppDataDirectory, "telegram");
    public static ISessionStore CreateSessionStore() => new KeychainSessionStore();
    public static string DeviceName => "TeleBlog for iOS";
    public static bool HasArgument(string argument) => Foundation.NSProcessInfo.ProcessInfo.Arguments.Contains(argument);
#endif
    public static async Task<TelegramConfig?> ReadConfigAsync()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
#if WINDOWS
        if(int.TryParse(Environment.GetEnvironmentVariable("TELEGRAM_API_ID"), out var id))
            return new(id, Environment.GetEnvironmentVariable("TELEGRAM_API_HASH") ?? "");
        if(new ProtectedSessionStore(Path.Combine(DataRoot,"app.config")).Load() is {} bytes)
            return JsonSerializer.Deserialize<TelegramConfig>(bytes, json);
        var sidecar = Path.Combine(AppContext.BaseDirectory, "telegram.json");
        if(File.Exists(sidecar)) return JsonSerializer.Deserialize<TelegramConfig>(await File.ReadAllTextAsync(sidecar), json);
#endif
        if(!await FileSystem.AppPackageFileExistsAsync("telegram.json")) return null;
        await using var stream = await FileSystem.OpenAppPackageFileAsync("telegram.json");
        return await JsonSerializer.DeserializeAsync<TelegramConfig>(stream, json);
    }
}
