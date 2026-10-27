using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class QrLoginTests
{
    [Fact] public void QrRendererAcceptsOnlyTelegramLoginTokens()
    {
        Assert.Throws<ArgumentException>(()=>TeleGramBlog.Telegram.LoginQrImage.Encode("https://example.com"));
        var png=TeleGramBlog.Telegram.LoginQrImage.Encode("tg://login?token=fixture-only");
        Assert.Equal(new byte[]{137,80,78,71,13,10,26,10},png.Take(8));
    }
    [Fact] public async Task TokenRenewalReplacesQrAndSuccessClearsIt()
    {
        var flow=new QrLoginFlow();
        await flow.RunAsync(async (show,password,ct)=>{
            show("tg://login?token=first");Assert.Equal("tg://login?token=first",flow.Url);
            show("tg://login?token=second");Assert.Equal("tg://login?token=second",flow.Url);await Task.CompletedTask;
        });
        Assert.Null(flow.Url);Assert.False(flow.IsRunning);
    }
    [Fact] public async Task TwoFactorWaitsForInputAndCanRequestRetry()
    {
        var flow=new QrLoginFlow();
        await flow.RunAsync(async (show,password,ct)=>{
            show("tg://login?token=test");
            var first=password(ct);Assert.True(flow.NeedsPassword);Assert.Null(flow.Url);
            flow.SubmitPassword("first");Assert.Equal("first",await first);
            var retry=password(ct);Assert.True(flow.NeedsPassword);flow.SubmitPassword("second");Assert.Equal("second",await retry);
        });
        Assert.False(flow.NeedsPassword);
    }
    [Fact] public async Task CancelUnblocksPasswordAndIgnoresLateTokens()
    {
        var flow=new QrLoginFlow();Action<string>? callback=null;
        var pending=flow.RunAsync(async(show,password,ct)=>{callback=show;await password(ct);});
        flow.Stop();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
        callback!("tg://login?token=late");Assert.Null(flow.Url);Assert.False(flow.NeedsPassword);Assert.False(flow.IsRunning);
    }
    [Fact] public async Task CanceledRunCannotFinishAsAuthenticatedWhenTransportIgnoresCancellation()
    {
        var flow=new QrLoginFlow();var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=flow.RunAsync((show,password,ct)=>completion.Task);flow.Stop();completion.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
    }
}
