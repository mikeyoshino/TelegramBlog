using System.Text.Json;
using Luma.Services;
namespace TeleGramBlog.Tests;

public sealed class SqliteFavoritesTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(), "luma-favorites-test-" + Guid.NewGuid());
    static MobilePost Post(string title = "Saved photo") => new("8:12", "8", "1", "Group", "Topic", title,
        DateTime.UnixEpoch, null, [], [new("8:12", "photo")]);
    string Image()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "image.jpg"); File.WriteAllBytes(path, [1, 2, 3, 4]); return path;
    }
    [Fact] public void ImagesAndFavoritesSurviveReopenAndRemovalDeletesBlobs()
    {
        var directory = Path.Combine(root, "account-a");
        var store = new SqliteFavoritesStore(directory); store.Load();
        var post = Post();
        store.Apply(new Dictionary<string,MobilePost>{{post.Id, post}}, [new(post.Id, "8:12", true, Image())]);
        var reopened = new SqliteFavoritesStore(directory);
        Assert.Equal(post.Title, reopened.Load()[post.Id].Title);
        Assert.Equal(new byte[]{1,2,3,4}, reopened.ReadMedia(post.Id, "8:12", true));
        Assert.Null(reopened.ReadMedia(post.Id, "8:12", false));
        Assert.StartsWith("SQLite format 3", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(store.DatabasePath), 0, 16));
        reopened.Apply(new Dictionary<string,MobilePost>(), []);
        Assert.Empty(reopened.Load()); Assert.False(reopened.HasMedia(post.Id, "8:12", true));
        reopened.PutMediaIfSaved(post.Id, "8:12", true, [9]);
        Assert.False(reopened.HasMedia(post.Id, "8:12", true));
        Assert.Empty(new SqliteFavoritesStore(Path.Combine(root, "account-b")).Load());
    }
    [Fact] public void LegacyJsonMigratesOnceWithoutResurrectingRemovedFavorites()
    {
        Directory.CreateDirectory(root);
        var post = Post(); var legacy = Path.Combine(root, "favorites.json");
        File.WriteAllText(legacy, JsonSerializer.Serialize(new Dictionary<string,MobilePost>{{post.Id,post}}, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var store = new SqliteFavoritesStore(root);
        Assert.Equal(post.Title, store.Load()[post.Id].Title);
        store.Apply(new Dictionary<string,MobilePost>(), []);
        Assert.Empty(new SqliteFavoritesStore(root).Load());
        Assert.True(File.Exists(legacy));
    }
    [Fact] public void FailedImageImportRollsBackMetadataAndExistingImage()
    {
        var store = new SqliteFavoritesStore(root); store.Load(); var post = Post();
        store.Apply(new Dictionary<string,MobilePost>{{post.Id,post}}, [new(post.Id, "8:12", true, Image())]);
        Assert.Throws<FileNotFoundException>(()=>store.Apply(new Dictionary<string,MobilePost>{{post.Id,Post("Changed")}},
            [new(post.Id, "8:12", true, Path.Combine(root,"missing.jpg"))]));
        Assert.Equal(post.Title, store.Load()[post.Id].Title);
        Assert.Equal(new byte[]{1,2,3,4}, store.ReadMedia(post.Id,"8:12",true));
    }
    [Fact] public void OrganizationSurvivesMovesAndCollectionDeletionWithoutLosingMedia()
    {
        var store = new SqliteFavoritesStore(root); var post = Post(); store.Load();
        store.Apply(new Dictionary<string,MobilePost>{{post.Id,post}}, [new(post.Id,post.Id,true,Image())]);
        var a = store.SaveCollection(null,"เรียน"); var b = store.SaveCollection(null,"บ้าน");
        store.SaveOrganization(post.Id,a,"กลับมาทบทวน",true);
        store.SaveOrganization(post.Id,b,"กลับมาทบทวน",true);
        var reopened = new SqliteFavoritesStore(root);
        var item = Assert.Single(reopened.LoadOrganization().Items);
        Assert.Equal(b,item.CollectionId); Assert.True(item.Viewed); Assert.Equal("กลับมาทบทวน",item.Note);
        store.Apply(new Dictionary<string,MobilePost>{{post.Id,Post("Refreshed")}}, []);
        Assert.Equal("กลับมาทบทวน",Assert.Single(store.LoadOrganization().Items).Note);
        store.DeleteCollection(b);
        item = Assert.Single(store.LoadOrganization().Items);
        Assert.Null(item.CollectionId); Assert.True(item.Viewed); Assert.Equal("กลับมาทบทวน",item.Note);
        Assert.True(store.HasMedia(post.Id,post.Id,true));
        Assert.Throws<ArgumentException>(()=>store.SaveCollection(null," เรียน "));
        Assert.Throws<ArgumentException>(()=>store.SaveCollection(null," "));
        Assert.Throws<ArgumentException>(()=>store.SaveOrganization(post.Id,"missing","",false));
        Assert.Empty(new SqliteFavoritesStore(Path.Combine(root,"other")).LoadOrganization().Items);
        store.Apply(new Dictionary<string,MobilePost>(), []);
        Assert.Empty(store.LoadOrganization().Items);
    }
    [Fact] public void SavingToCollectionIsAtomicAndPreservesExistingNotes()
    {
        var store = new SqliteFavoritesStore(root);store.Load();var post=Post();
        var category=store.SaveCollection(null,"เรียน");
        store.Apply(new Dictionary<string,MobilePost>{{post.Id,post}},[new(post.Id,post.Id,true,Image())],[post.Id],category);
        Assert.Equal(category,Assert.Single(new SqliteFavoritesStore(root).LoadOrganization().Items).CollectionId);
        store.SaveOrganization(post.Id,category,"Keep note",true);
        var next=store.SaveCollection(null,"Later");
        store.Apply(new Dictionary<string,MobilePost>{{post.Id,post}},[],[post.Id],next);
        var detail=Assert.Single(store.LoadOrganization().Items);
        Assert.Equal(next,detail.CollectionId);Assert.Equal("Keep note",detail.Note);Assert.True(detail.Viewed);
        Assert.Throws<ArgumentException>(()=>store.Apply(new Dictionary<string,MobilePost>{{post.Id,Post("Must rollback")}},[],[post.Id],"deleted"));
        Assert.Equal(post.Title,store.Load()[post.Id].Title);
        Assert.Equal(next,Assert.Single(store.LoadOrganization().Items).CollectionId);
        Assert.True(store.HasMedia(post.Id,post.Id,true));
    }
    public void Dispose() { if(Directory.Exists(root))Directory.Delete(root,true); }
}
