using TeleGramBlog.App.ViewModels;
using TeleGramBlog.Core;
namespace TeleGramBlog.Tests;
public class ShellTests
{
    [Fact] public async Task LongListsAndEmptyOverviewHaveRealCountsAndScopedStates()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();
        var longName=new string('ก',180);
        vm.SetVerificationGroups(Enumerable.Range(1,80).Select(i=>new GroupInfo(i,longName+i,true)));
        Assert.Equal(80,vm.Groups.Count);await vm.SelectGroupAsync(vm.Groups[79]);
        vm.SetVerificationTopics(Enumerable.Range(1,80).Select(i=>new TopicInfo(i,longName+i)));
        Assert.Equal(80,vm.Topics.Count);Assert.False(vm.ShowTopicsEmpty);
        vm.TopicQuery="not found";Assert.True(vm.ShowTopicsEmpty);Assert.False(vm.ShowMediaEmpty);
        vm.SetVerificationTopics([]);Assert.True(vm.ShowTopicsEmpty);vm.Cancel();Assert.False(vm.ShowTopicsEmpty);Assert.True(vm.ShowCanceled);
        vm.SetVerificationGroups([]);Assert.True(vm.ShowGroupsEmpty);Assert.False(vm.ShowTopicsEmpty);
    }
    [Fact] public async Task BackToTopicRestoresPageAndCancelRefreshRetainsSearchableHistory()
    {
        var pending=new TaskCompletionSource<HistoryBatch>();var block=false;
        using var vm=new MainViewModel(historySourceFactory:(_,_)=>block?new TestSource(()=>pending.Task):new DemoHistorySource());
        await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);var topic=vm.Topics[0];await vm.SelectTopicAsync(topic);await vm.NextAsync();
        var first=vm.Cards[0].Post.Id;vm.BackToOverview();await vm.SelectTopicAsync(topic);Assert.Equal(first,vm.Cards[0].Post.Id);
        block=true;var refresh=vm.RefreshAsync();Assert.True(vm.IsBusy);vm.PageSize=48;Assert.Equal(24,vm.PageSize);
        vm.Cancel();await refresh;Assert.Equal(54,vm.LoadedCount);
        vm.PostQuery="Coastal";Assert.NotEmpty(vm.Cards);Assert.False(vm.ShowSearchEmpty);
    }
    [Fact] public async Task InitialLoadingEmptyFailureAndCancellationAreMutuallyExclusive()
    {
        var pending=new TaskCompletionSource<HistoryBatch>();
        using var vm=new MainViewModel(historySourceFactory:(_,_)=>new TestSource(()=>pending.Task));
        await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);
        var load=vm.SelectTopicAsync(vm.Topics[0]);
        Assert.True(vm.ShowFirstLoading);Assert.False(vm.ShowMediaEmpty);Assert.False(vm.ShowSearchEmpty);
        pending.SetResult(new([],0,true));await load;Assert.True(vm.ShowMediaEmpty);Assert.False(vm.ShowFirstLoading);
        pending=new();load=vm.SelectTopicAsync(vm.Topics[1]);pending.SetException(new IOException("offline"));await load;
        Assert.True(vm.HasError);Assert.False(vm.ShowMediaEmpty);Assert.False(vm.ShowSearchEmpty);
        pending=new();load=vm.SelectTopicAsync(vm.Topics[0]);vm.Cancel();pending.SetResult(new([],0,true));await load;
        Assert.True(vm.ShowCanceled);Assert.False(vm.ShowMediaEmpty);
    }
    [Fact] public async Task RefreshKeepsExistingCardsAndNewTopicClearsThem()
    {
        var pending=new TaskCompletionSource<HistoryBatch>();
        var message=new HistoryMessage(1,0,DateTime.UtcNow,"post",[],new(1,1,"photo"));
        using var vm=new MainViewModel(historySourceFactory:(_,_)=>new TestSource(()=>pending.Task));
        await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);
        pending.SetResult(new([message],1,true));await vm.SelectTopicAsync(vm.Topics[0]);Assert.Single(vm.Cards);
        pending=new();var refresh=vm.RefreshAsync();Assert.Single(vm.Cards);Assert.True(vm.ShowBusyStrip);Assert.False(vm.ShowMediaEmpty);
        pending.SetResult(new([message],1,true));await refresh;
        pending=new();var change=vm.SelectTopicAsync(vm.Topics[1]);Assert.Empty(vm.Cards);Assert.True(vm.ShowFirstLoading);
        pending.SetResult(new([],0,true));await change;
    }
    sealed class TestSource(Func<Task<HistoryBatch>> fetch):IHistorySource
    {public Task<HistoryBatch> FetchAsync(int beforeId,CancellationToken ct)=>fetch().WaitAsync(ct);}
    [Fact] public async Task ForumStartsAtOverviewAndDirectGroupSkipsIt()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();
        Assert.True(vm.IsGroupPicker);
        await vm.SelectGroupAsync(vm.Groups[0]);Assert.True(vm.IsOverview);Assert.Null(vm.SelectedTopic);Assert.Empty(vm.Cards);
        await vm.SelectTopicAsync(vm.Topics[0]);Assert.True(vm.IsPosts);Assert.NotEmpty(vm.Cards);
        vm.BackToOverview();Assert.True(vm.IsOverview);Assert.Empty(vm.Cards);
        await vm.SelectGroupAsync(new GroupInfo(5,"No topics",false));Assert.True(vm.IsPosts);Assert.False(vm.IsOverview);
    }
    [Fact] public async Task SearchStateAndFooterReflectLoadedData()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[0]);
        Assert.True(vm.ShowPagination);Assert.Contains("54",vm.PageSummary);
        vm.PostQuery="nothing-matches-this";Assert.True(vm.ShowSearchEmpty);Assert.False(vm.ShowMediaEmpty);Assert.False(vm.ShowPagination);
        vm.PostQuery="";Assert.False(vm.ShowSearchEmpty);
        var before=vm.Cards.Select(c=>c.Post.Id).ToArray();vm.SortIndex=1;
        Assert.Equal(before.Order(),vm.Cards.Select(c=>c.Post.Id));
        vm.BackToOverview();Assert.False(vm.ShowMediaEmpty);Assert.False(vm.ShowSearchEmpty);
    }
    [Fact] public async Task GroupAndTopicQueriesSurviveBackNavigation()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();vm.GroupQuery="Creative";await vm.SelectGroupAsync(vm.Groups[0]);
        vm.TopicQuery="Design";await vm.SelectTopicAsync(vm.Topics[0]);vm.BackToOverview();Assert.Equal("Design",vm.TopicQuery);
        vm.BackToGroups();Assert.Equal("Creative",vm.GroupQuery);await vm.SelectGroupAsync(vm.Groups[0]);Assert.Equal("Design",vm.TopicQuery);
    }
}
