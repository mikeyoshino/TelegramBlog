using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class ViewerSessionTests
{
    [Fact] public void NavigationStopsAtEndsAndOpensClickedMedia()
    {
        using var session=new ViewerSession(3,1);
        Assert.Equal(1,session.Index);Assert.True(session.Select(2));
        Assert.False(session.Select(3));Assert.Equal(2,session.Index);
        Assert.False(session.Select(-1));Assert.True(session.Select(0));Assert.False(session.HasPrevious);
        using var single=new ViewerSession(1,99);Assert.Equal(0,single.Index);Assert.False(single.HasNext);
        using var empty=new ViewerSession(0);Assert.False(empty.Select(0));Assert.False(empty.HasNext);
    }
    [Fact] public void OldSuccessAndFailureCannotOverwriteNewMedia()
    {
        using var session=new ViewerSession(2);
        var first=session.Begin();session.Select(1);var second=session.Begin();
        Assert.True(first.IsCancellationRequested);Assert.False(session.Complete(first));Assert.False(session.Fail(first));
        Assert.Equal(ViewerLoadState.Loading,session.State);Assert.True(session.Complete(second));Assert.Equal(ViewerLoadState.Ready,session.State);
    }
    [Fact] public void CancelInvalidatesCallbacksAndRetryClearsError()
    {
        using var session=new ViewerSession(1);var token=session.Begin();session.Cancel();
        Assert.False(session.Complete(token));Assert.Equal(ViewerLoadState.Canceled,session.State);
        token=session.Begin();Assert.True(session.Fail(token));Assert.Equal(ViewerLoadState.Error,session.State);
        token=session.Begin();Assert.Equal(ViewerLoadState.Loading,session.State);Assert.True(session.Complete(token));
    }
}
