namespace TeleGramBlog.Core;
public sealed class QrLoginFlow
{
    readonly object gate=new();
    CancellationTokenSource? active;
    TaskCompletionSource<string>? passwordInput;
    public event Action? Changed;
    public string? Url {get;private set;}
    public bool NeedsPassword {get;private set;}
    public bool IsRunning {get;private set;}
    public async Task RunAsync(Func<Action<string>,Func<CancellationToken,Task<string>>,CancellationToken,Task> authenticate)
    {
        Stop();
        var run=new CancellationTokenSource();
        lock(gate){active=run;IsRunning=true;}
        Changed?.Invoke();
        try
        {
            await authenticate(url=>{
                lock(gate){if(active!=run||run.IsCancellationRequested)return;Url=url;}
                Changed?.Invoke();
            },ct=>RequestPasswordAsync(run,ct),run.Token);
            run.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            lock(gate){if(active==run){active=null;Url=null;NeedsPassword=false;IsRunning=false;passwordInput=null;}}
            run.Dispose();Changed?.Invoke();
        }
    }
    async Task<string> RequestPasswordAsync(CancellationTokenSource run,CancellationToken ct)
    {
        TaskCompletionSource<string> input;
        lock(gate)
        {
            if(active!=run)throw new OperationCanceledException(ct);
            input=new(TaskCreationOptions.RunContinuationsAsynchronously);
            passwordInput=input;NeedsPassword=true;Url=null;
        }
        Changed?.Invoke();
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(run.Token,ct);
        try{return await input.Task.WaitAsync(linked.Token);}
        finally
        {
            lock(gate){if(passwordInput==input){passwordInput=null;NeedsPassword=false;}}
            Changed?.Invoke();
        }
    }
    public void SubmitPassword(string password)
    {lock(gate){if(!string.IsNullOrWhiteSpace(password))passwordInput?.TrySetResult(password);}}
    public void Stop()
    {
        lock(gate)
        {
            var old=active;active=null;Url=null;NeedsPassword=false;IsRunning=false;passwordInput=null;
            old?.Cancel();
        }
        Changed?.Invoke();
    }
}
