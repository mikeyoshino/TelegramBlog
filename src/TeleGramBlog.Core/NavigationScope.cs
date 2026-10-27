namespace TeleGramBlog.Core;
public sealed class NavigationScope : IDisposable
{
    CancellationTokenSource current=new();
    public CancellationToken Token=>current.Token;
    public CancellationToken Next() {current.Cancel(); current.Dispose(); current=new(); return current.Token;}
    public bool IsCurrent(CancellationToken token)=>token==current.Token && !token.IsCancellationRequested;
    public void Dispose() {current.Cancel();current.Dispose();}
}
