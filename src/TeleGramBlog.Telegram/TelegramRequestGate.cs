using TL;
namespace TeleGramBlog.Telegram;

/// <summary>Honors server cooldown across history and media requests. Never retries a flood automatically.</summary>
public sealed class TelegramRequestGate(Func<DateTimeOffset>? clock=null)
{
    readonly Func<DateTimeOffset> now=clock??(()=>DateTimeOffset.UtcNow);
    readonly object sync=new();
    readonly SemaphoreSlim pacing=new(1);
    DateTimeOffset blockedUntil;
    DateTimeOffset nextHistory;
    public async Task<T> RunAsync<T>(Func<Task<T>> request,CancellationToken ct,bool pace=false)
    {
        ct.ThrowIfCancellationRequested();Check();
        if(pace)
        {
            await pacing.WaitAsync(ct);
            try
            {
                var wait=nextHistory-now();
                if(wait>TimeSpan.Zero)await Task.Delay(wait,ct);
                Check();nextHistory=now().AddMilliseconds(500);
            }
            finally{pacing.Release();}
        }
        ct.ThrowIfCancellationRequested();Check();
        try{return await request();}
        catch(RpcException e) when(IsFlood(e))
        {
            lock(sync){var until=now().AddSeconds(Math.Max(1,e.X));if(until>blockedUntil)blockedUntil=until;}
            throw;
        }
    }
    void Check()
    {
        lock(sync)
        {
            var seconds=(int)Math.Ceiling((blockedUntil-now()).TotalSeconds);
            if(seconds>0)throw new RpcException(420,"FLOOD_WAIT_X",seconds);
        }
    }
    public static bool IsFlood(RpcException e)=>e.Code==420&&(e.Message.StartsWith("FLOOD_WAIT_")||e.Message.StartsWith("FLOOD_PREMIUM_WAIT_"));
}
