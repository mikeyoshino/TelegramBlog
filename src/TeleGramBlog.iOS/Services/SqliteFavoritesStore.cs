using System.Text.Json;
using SQLite;
namespace Luma.Services;

// Short-lived connections let logout remove the entire account directory safely.
public sealed class SqliteFavoritesStore(string accountDirectory)
{
    static SqliteFavoritesStore()
    {
#if WINDOWS
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
#else
#if !IOS
        if(OperatingSystem.IsWindows()) SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        else
#endif
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
#endif
    }
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly object gate = new();
    public string DatabasePath => Path.Combine(accountDirectory, "favorites.sqlite3");
    SQLiteConnection Open()
    {
        Directory.CreateDirectory(accountDirectory);
        var db = new SQLiteConnection(DatabasePath);
        try
        {
            db.Execute("PRAGMA foreign_keys=ON");
            db.Execute("CREATE TABLE IF NOT EXISTS favorites (id TEXT PRIMARY KEY NOT NULL, payload TEXT NOT NULL)");
            db.Execute("CREATE TABLE IF NOT EXISTS favorite_media (post_id TEXT NOT NULL REFERENCES favorites(id) ON DELETE CASCADE, media_id TEXT NOT NULL, original INTEGER NOT NULL, bytes BLOB NOT NULL, PRIMARY KEY(post_id,media_id,original))");
            db.Execute("CREATE TABLE IF NOT EXISTS migration (name TEXT PRIMARY KEY NOT NULL)");
            db.Execute("CREATE TABLE IF NOT EXISTS collections (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL)");
            db.Execute("CREATE TABLE IF NOT EXISTS favorite_details (post_id TEXT PRIMARY KEY NOT NULL REFERENCES favorites(id) ON DELETE CASCADE, collection_id TEXT REFERENCES collections(id) ON DELETE SET NULL, note TEXT NOT NULL DEFAULT '', viewed INTEGER NOT NULL DEFAULT 0)");
            return db;
        }
        catch { db.Dispose(); throw; }
    }
    public Dictionary<string, MobilePost> Load()
    {
        lock(gate)
        {
            using var db = Open();
            if(db.ExecuteScalar<int>("SELECT count(*) FROM migration WHERE name=?", "favorites-json") == 0)
            {
                var legacy = Path.Combine(accountDirectory, "favorites.json");
                var posts = File.Exists(legacy)
                    ? JsonSerializer.Deserialize<Dictionary<string,MobilePost>>(File.ReadAllText(legacy), Json)
                        ?? throw new IOException("Cannot migrate the saved favorites file.")
                    : new Dictionary<string,MobilePost>();
                db.RunInTransaction(() => {
                    foreach(var post in posts.Values)
                        db.Execute("INSERT OR IGNORE INTO favorites(id,payload) VALUES(?,?)", post.Id, JsonSerializer.Serialize(post, Json));
                    db.Execute("INSERT INTO migration(name) VALUES(?)", "favorites-json");
                });
                // Preserve the old file as a recovery source; the migration marker prevents resurrection.
            }
            return db.Query<FavoriteRow>("SELECT payload FROM favorites")
                .Select(row => JsonSerializer.Deserialize<MobilePost>(row.Payload, Json) ?? throw new IOException("Invalid saved post."))
                .ToDictionary(post => post.Id);
        }
    }
    public byte[]? ReadMedia(string postId, string mediaId, bool original)
    {
        lock(gate)
        {
            using var db = Open();
            return db.ExecuteScalar<byte[]?>("SELECT bytes FROM favorite_media WHERE post_id=? AND media_id=? AND original=?", postId, mediaId, original ? 1 : 0);
        }
    }
    public bool HasMedia(string postId, string mediaId, bool original)
    {
        lock(gate)
        {
            using var db = Open();
            return db.ExecuteScalar<int>("SELECT count(*) FROM favorite_media WHERE post_id=? AND media_id=? AND original=?", postId, mediaId, original ? 1 : 0) > 0;
        }
    }
    public void PutMediaIfSaved(string postId, string mediaId, bool original, byte[] bytes)
    {
        lock(gate)
        {
            using var db = Open();
            db.Execute("INSERT OR REPLACE INTO favorite_media(post_id,media_id,original,bytes) SELECT ?,?,?,? WHERE EXISTS (SELECT 1 FROM favorites WHERE id=?)",
                postId, mediaId, original ? 1 : 0, bytes, postId);
        }
    }
    public void Apply(IReadOnlyDictionary<string,MobilePost> posts, IReadOnlyList<FavoriteMediaFile> files, IReadOnlyCollection<string>? assignedIds = null, string? collectionId = null)
    {
        lock(gate)
        {
            using var db = Open();
            db.RunInTransaction(() => {
                foreach(var existing in db.Query<FavoriteId>("SELECT id FROM favorites"))
                    if(!posts.ContainsKey(existing.Id)) db.Execute("DELETE FROM favorites WHERE id=?", existing.Id);
                foreach(var post in posts.Values)
                    db.Execute("INSERT INTO favorites(id,payload) VALUES(?,?) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", post.Id, JsonSerializer.Serialize(post, Json));
                if(assignedIds != null)
                {
                    if(collectionId == null || db.ExecuteScalar<int>("SELECT count(*) FROM collections WHERE id=?",collectionId)==0)
                        throw new ArgumentException("กรุณาเลือกคอลเลกชันที่มีอยู่");
                    foreach(var id in assignedIds)
                        db.Execute("INSERT INTO favorite_details(post_id,collection_id,note,viewed) VALUES(?,?,'',0) ON CONFLICT(post_id) DO UPDATE SET collection_id=excluded.collection_id",id,collectionId);
                }
                foreach(var file in files)
                    db.Execute("INSERT OR REPLACE INTO favorite_media(post_id,media_id,original,bytes) VALUES(?,?,?,?)", file.PostId, file.MediaId, file.Original ? 1 : 0, File.ReadAllBytes(file.Path));
            });
        }
    }
    public OrganizationSnapshot LoadOrganization()
    {
        lock(gate)
        {
            using var db = Open();
            return new(db.Query<CollectionRow>("SELECT id,name FROM collections ORDER BY name"),
                db.Query<OrganizationRow>("SELECT post_id AS PostId,collection_id AS CollectionId,note,viewed FROM favorite_details"));
        }
    }
    public string SaveCollection(string? id, string name)
    {
        name = name.Trim();
        if(name.Length is 0 or > 80) throw new ArgumentException("ชื่อหมวดต้องมี 1–80 ตัวอักษร");
        lock(gate)
        {
            using var db = Open();
            var rows = db.Query<CollectionRow>("SELECT id,name FROM collections");
            if(rows.Any(r => r.Id != id && string.Equals(r.Name,name,StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("มีหมวดชื่อนี้แล้ว");
            if(id != null && !rows.Any(r=>r.Id==id)) throw new ArgumentException("ไม่พบหมวดนี้");
            id ??= Guid.NewGuid().ToString("N");
            db.Execute("INSERT INTO collections(id,name) VALUES(?,?) ON CONFLICT(id) DO UPDATE SET name=excluded.name",id,name);
            return id;
        }
    }
    public void DeleteCollection(string id)
    {
        lock(gate) { using var db = Open(); db.Execute("DELETE FROM collections WHERE id=?",id); }
    }
    public void SaveOrganization(string postId, string? collectionId, string note, bool viewed)
    {
        if(note.Length > 4000) throw new ArgumentException("โน้ตยาวได้ไม่เกิน 4,000 ตัวอักษร");
        lock(gate)
        {
            using var db = Open();
            db.RunInTransaction(()=> {
                if(db.ExecuteScalar<int>("SELECT count(*) FROM favorites WHERE id=?",postId)==0) throw new ArgumentException("กรุณาเก็บข้อความนี้ก่อน");
                if(collectionId != null && db.ExecuteScalar<int>("SELECT count(*) FROM collections WHERE id=?",collectionId)==0) throw new ArgumentException("ไม่พบหมวดนี้");
                db.Execute("INSERT INTO favorite_details(post_id,collection_id,note,viewed) VALUES(?,?,?,?) ON CONFLICT(post_id) DO UPDATE SET collection_id=excluded.collection_id,note=excluded.note,viewed=excluded.viewed",postId,collectionId,note,viewed?1:0);
            });
        }
    }
    public sealed class CollectionRow { public string Id { get; set; } = ""; public string Name { get; set; } = ""; }
    public sealed class OrganizationRow
    {
        public string PostId { get; set; } = "";
        public string? CollectionId { get; set; }
        public string Note { get; set; } = "";
        public bool Viewed { get; set; }
    }
    public sealed record OrganizationSnapshot(List<CollectionRow> Collections, List<OrganizationRow> Items);
    public sealed class FavoriteRow { public string Payload { get; set; } = ""; }
    public sealed class FavoriteId { public string Id { get; set; } = ""; }
}
public sealed record FavoriteMediaFile(string PostId, string MediaId, bool Original, string Path);
