using System.Diagnostics;
using System.Text.Json;
namespace Luma.Services;

// Debug-only health evidence. Never includes account IDs, phone numbers, codes or session contents.
internal static class LifecycleDiagnostics
{
    [Conditional("DEBUG")]
    public static void Record(string stage, bool authenticated)
    {
        try
        {
            File.WriteAllText(Path.Combine(FileSystem.CacheDirectory, "lifecycle-health.json"),
                JsonSerializer.Serialize(new { stage, authenticated, internet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet, at = DateTimeOffset.UtcNow }));
        }
        catch(IOException) { }
    }
}
