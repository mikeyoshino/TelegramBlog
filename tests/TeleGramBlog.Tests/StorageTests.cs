using TeleGramBlog.Telegram;
namespace TeleGramBlog.Tests;
public class StorageTests
{
    [Fact] public void SessionProtectedAtRestCanOverwriteAndClear()
    {
        var dir=Path.Combine(Path.GetTempPath(),"TeleGramBlogTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try {
            var path=Path.Combine(dir,"session"); var store=new ProtectedSessionStore(path);
            store.Save([1,2,3,4]); Assert.Equal(new byte[]{1,2,3,4},store.Load()); Assert.NotEqual(new byte[]{1,2,3,4},File.ReadAllBytes(path));
            store.Save([9]); Assert.Equal(new byte[]{9},store.Load()); store.Clear(); Assert.Null(store.Load());
        } finally { Directory.Delete(dir,true); }
    }
}
