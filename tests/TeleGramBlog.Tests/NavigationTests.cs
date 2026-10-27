using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class NavigationTests
{
    [Fact] public void NewNavigationInvalidatesLateResponse()
    {
        using var scope=new NavigationScope(); var first=scope.Next(); var second=scope.Next();
        Assert.True(first.IsCancellationRequested); Assert.False(scope.IsCurrent(first)); Assert.True(scope.IsCurrent(second));
    }
}
