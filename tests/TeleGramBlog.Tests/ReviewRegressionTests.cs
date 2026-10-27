using TeleGramBlog.Core;
using TeleGramBlog.Telegram;
using TL;
namespace TeleGramBlog.Tests;
public class ReviewRegressionTests
{
    [Fact] public async Task CachedPageIsReadableWithoutSuccessfulNetworkFetch()
    {
        var snapshot=new HistorySnapshot([new(24,0,DateTime.UnixEpoch,"saved",["https://example.com"],null)],24,false);
        var pager=new HistoryPager(new OfflineSource(),snapshot);
        Assert.Single(pager.GetCachedPage(1,24).Cards);
        await Assert.ThrowsAsync<IOException>(()=>pager.GetPageAsync(1,24));
        Assert.Single(pager.GetCachedPage(1,24).Cards);
    }
    [Fact] public void ProgressiveOriginalIsSelectedForFullResolution()
    {
        var large=new PhotoSizeProgressive {type="y",w=1200,h=800,sizes=[100,400]};
        var photo=new Photo {sizes=[new PhotoSize {type="m",w=320,h=200,size=50},large]};
        Assert.Same(large,MediaSelection.SelectPhotoSize(photo,true));
    }
    [Fact] public void LockedSessionDoesNotPreventCacheCleanupAndReportsFailure()
    {
        var root=Path.Combine(Path.GetTempPath(),"TeleGramBlogTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);var store=new LocalStore(root);var session=new ProtectedSessionStore(Path.Combine(root,"account.session"));
        session.Save([1,2,3]);Directory.CreateDirectory(store.AccountPath(12));File.WriteAllText(Path.Combine(store.AccountPath(12),"cached"),"private");
        try {
            using(var locked=new FileStream(Path.Combine(root,"account.session"),FileMode.Open,FileAccess.Read,FileShare.None))
            {var result=store.ClearLocalData(session);Assert.False(result);Assert.False(Directory.Exists(store.AccountPath(12)));}
            Assert.True(store.ClearLocalData(session));Assert.Null(session.Load());
        }finally{Directory.Delete(root,true);}
    }
    sealed class OfflineSource:IHistorySource {public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken cancellationToken)=>throw new IOException("offline");}
}
