using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class PhoneLoginTests
{
    [Fact] public async Task PhoneFallbackAsksForCodeThenPassword()
    {
        using var ct=new CancellationTokenSource();PhoneLoginFlow flow=null!;
        flow=new(async()=>{Assert.Equal("12345",await flow.RequestInputAsync("verification_code"));Assert.Equal("secret",await flow.RequestInputAsync("password"));},ct.Token);
        Assert.Equal("verification_code",await flow.AdvanceAsync("+660000"));
        Assert.Equal("password",await flow.AdvanceAsync("12345"));
        Assert.Null(await flow.AdvanceAsync("secret"));
    }
    [Fact] public async Task CancelReleasesAbandonedInputPrompt()
    {
        using var ct=new CancellationTokenSource();PhoneLoginFlow flow=null!;
        var released=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        flow=new(async()=>{try{await flow.RequestInputAsync("verification_code");}finally{released.SetResult();}},ct.Token);
        Assert.Equal("verification_code",await flow.AdvanceAsync("+660000"));ct.Cancel();
        await released.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>flow.AdvanceAsync("late"));
    }
}
