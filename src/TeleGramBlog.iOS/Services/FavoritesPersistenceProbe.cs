#if DEBUG
using System.Text.Json;
namespace Luma.Services;
internal static class FavoritesPersistenceProbe
{
    public static void Run(bool write)
    {
        var root = Path.Combine(FileSystem.CacheDirectory, "favorites-persistence-check");
        var report = Path.Combine(FileSystem.CacheDirectory, "favorites-persistence-probe.json");
        try
        {
            var store = new SqliteFavoritesStore(root);
            store.Load();
            if(write)
            {
                var photo = Path.Combine(root, "synthetic.bin");
                File.WriteAllBytes(photo, [76, 85, 77, 65]);
                var post = new MobilePost("1:1", "1", "1", "Synthetic", "Synthetic", "Synthetic",
                    DateTime.UnixEpoch, null, [], [new("1:1", "photo")]);
                store.Apply(new Dictionary<string,MobilePost>{{post.Id,post}}, [new(post.Id,"1:1",true,photo)]);
                File.Delete(photo);
            }
            var passed = store.Load().ContainsKey("1:1") && store.ReadMedia("1:1","1:1",true)?.SequenceEqual(new byte[]{76,85,77,65}) == true;
            File.WriteAllText(report, JsonSerializer.Serialize(new {stage=write?"write":"read-after-relaunch",passed}));
            if(!write && passed) Directory.Delete(root,true);
        }
        catch(Exception e) { File.WriteAllText(report, JsonSerializer.Serialize(new {stage=write?"write":"read-after-relaunch",passed=false,error=e.Message})); }
    }
}
#endif
