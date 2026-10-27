namespace TeleGramBlog.Core;
public enum ViewerLoadState { Idle, Loading, Ready, Error, Canceled }
public sealed class ViewerSession(int count,int initialIndex=0):IDisposable
{
    readonly NavigationScope scope=new();
    public int Count {get;}=Math.Max(0,count);
    public int Index {get;private set;}=Math.Clamp(initialIndex,0,Math.Max(0,count-1));
    public bool HasPrevious=>Count>0&&Index>0;
    public bool HasNext=>Index+1<Count;
    public ViewerLoadState State {get;private set;}
    public bool Select(int index){if(index<0||index>=Count||index==Index)return false;scope.Next();Index=index;State=ViewerLoadState.Idle;return true;}
    public CancellationToken Begin(){var token=scope.Next();State=ViewerLoadState.Loading;return token;}
    public bool IsCurrent(CancellationToken token)=>scope.IsCurrent(token);
    public bool Complete(CancellationToken token){if(!IsCurrent(token))return false;State=ViewerLoadState.Ready;return true;}
    public bool Fail(CancellationToken token){if(!IsCurrent(token))return false;State=ViewerLoadState.Error;return true;}
    public void Cancel(){scope.Next();State=ViewerLoadState.Canceled;}
    public void Dispose()=>scope.Dispose();
}
