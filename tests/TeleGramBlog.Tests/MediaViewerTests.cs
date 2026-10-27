using TeleGramBlog.Telegram;
using TL;
namespace TeleGramBlog.Tests;
public class MediaViewerTests
{
    [Fact] public async Task DisposalRetriesUntilDecoderReleasesItsHandle()
    {
        var root=NewFolder();
        try
        {
            var file=await TemporaryMediaFile.DownloadAsync(root,".mp4",async(s,ct)=>await s.WriteAsync(new byte[]{1,2},ct),default);
            using(var decoder=new FileStream(file.Path,FileMode.Open,FileAccess.Read,FileShare.Read)){file.Dispose();Assert.True(File.Exists(file.Path));}
            for(var i=0;i<15&&File.Exists(file.Path);i++)await Task.Delay(100);
            Assert.False(File.Exists(file.Path));
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public async Task CompletedVideoLivesUntilViewerReleasesIt()
    {
        var root=NewFolder();
        try
        {
            var file=await TemporaryMediaFile.DownloadAsync(root,".mp4",async(s,ct)=>await s.WriteAsync(new byte[]{1,2,3},ct),default);
            Assert.EndsWith(".mp4",file.Path);Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(file.Path));
            file.Dispose();Assert.Empty(Directory.GetFiles(root));
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public async Task CancellationNeverLeavesPartialVideoBehind()
    {
        var root=NewFolder();using var ct=new CancellationTokenSource();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>TemporaryMediaFile.DownloadAsync(root,".mp4",async(s,t)=>{await s.WriteAsync(new byte[]{1,2,3},t);ct.Cancel();},ct.Token));
            Assert.Empty(Directory.GetFiles(root));
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public async Task FailedDownloadCleansPartialFile()
    {
        var root=NewFolder();
        try
        {
            await Assert.ThrowsAsync<IOException>(()=>TemporaryMediaFile.DownloadAsync(root,".mp4",async(s,ct)=>{await s.WriteAsync(new byte[]{1},ct);throw new IOException("network failed");},default));
            Assert.Empty(Directory.GetFiles(root));
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public void VideoDocumentsStayVideosWithinAlbums()
    {
        var m=MessageMapper.Map(new Message {id=7,grouped_id=99,message="video",media=new MessageMediaDocument {document=new Document {mime_type="video/mp4"}}},12);
        Assert.Equal("video",m.Media!.Kind);Assert.Equal(99,m.AlbumId);
    }
    static string NewFolder(){var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"TeleGramBlogTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
}
