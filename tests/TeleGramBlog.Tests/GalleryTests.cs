using TeleGramBlog.Core;
using TeleGramBlog.App.ViewModels;
namespace TeleGramBlog.Tests;
public class GalleryTests
{
    [Fact] public async Task MixedAlbumCrossingFetchBoundaryIsOneCardInOriginalMediaOrder()
    {
        var pager=new HistoryPager(new AlbumSource());
        var page=await pager.GetPageAsync(1,24);
        var post=Assert.Single(page.Cards);
        Assert.Equal(new[]{10,11,12},post.Media.Select(m=>m.MessageId));
        var card=new CardViewModel(post);
        Assert.Equal("View album",card.ViewLabel);
        Assert.Contains("video",card.Badge);Assert.Contains("2 photos",card.Badge);
        Assert.DoesNotContain("3 images",post.Domain);
    }
    sealed class AlbumSource:IHistorySource
    {
        public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken ct)=>Task.FromResult(beforeId==0
            ?new HistoryBatch([Message(12,"photo"),Message(11,"video")],11,false)
            :new HistoryBatch([Message(10,"photo")],10,true));
        static HistoryMessage Message(int id,string kind)=>new(id,99,DateTime.Today,"Album",[],new(1,id,kind));
    }
}
