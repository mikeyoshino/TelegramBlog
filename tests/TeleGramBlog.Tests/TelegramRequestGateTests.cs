using TeleGramBlog.Telegram;
using TL;
namespace TeleGramBlog.Tests;
public class TelegramRequestGateTests
{
    [Fact] public void FloodMessageUsesNumericExceptionProperty()
    {
        Assert.Contains("37 seconds",TelegramService.Explain(new RpcException(420,"FLOOD_WAIT_X",37)));
    }
    [Fact] public async Task CooldownBlocksCallsUntilServerDeadline()
    {
        var now=DateTimeOffset.UtcNow;
        var gate=new TelegramRequestGate(()=>now);
        await Assert.ThrowsAsync<RpcException>(()=>gate.RunAsync<int>(()=>throw new RpcException(420,"FLOOD_WAIT_X",37),default));
        var called=false;
        now=now.AddSeconds(10);
        var error=await Assert.ThrowsAsync<RpcException>(()=>gate.RunAsync(()=>{called=true;return Task.FromResult(1);},default));
        Assert.Equal(27,error.X);Assert.False(called);
        now=now.AddSeconds(27);
        Assert.Equal(1,await gate.RunAsync(()=>{called=true;return Task.FromResult(1);},default));Assert.True(called);
    }
    [Fact] public async Task CanceledRequestDoesNotReachNetwork()
    {
        var gate=new TelegramRequestGate();var called=false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>gate.RunAsync(()=>{called=true;return Task.FromResult(1);},new CancellationToken(true),true));
        Assert.False(called);
    }
}
