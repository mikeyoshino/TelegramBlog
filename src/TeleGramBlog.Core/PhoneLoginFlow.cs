namespace TeleGramBlog.Core;
public sealed class PhoneLoginFlow(Func<Task> authenticate,CancellationToken lifetime)
{
    readonly object gate=new();
    TaskCompletionSource<string?> next=new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<string>? response;
    bool started;
    public Task<string?> AdvanceAsync(string input)
    {
        lifetime.ThrowIfCancellationRequested();
        bool launch=false;Task<string?> result;
        lock(gate)
        {
            if(!started){started=true;launch=true;}
            else if(response!=null)
            {
                var pending=response;response=null;next=new(TaskCreationOptions.RunContinuationsAsynchronously);
                pending.TrySetResult(input);
            }
            result=next.Task;
        }
        if(launch)_=RunAsync();
        return result.WaitAsync(lifetime);
    }
    async Task RunAsync()
    {
        try{await authenticate();lifetime.ThrowIfCancellationRequested();lock(gate)next.TrySetResult(null);}
        catch(OperationCanceledException){lock(gate)next.TrySetCanceled(lifetime);}
        catch(Exception e){lock(gate)next.TrySetException(e);}
    }
    public Task<string> RequestInputAsync(string key)
    {
        lifetime.ThrowIfCancellationRequested();
        lock(gate)
        {
            response=new(TaskCreationOptions.RunContinuationsAsynchronously);
            next.TrySetResult(key);
            return response.Task.WaitAsync(lifetime);
        }
    }
}
