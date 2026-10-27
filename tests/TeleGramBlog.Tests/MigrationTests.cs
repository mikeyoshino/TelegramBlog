using TeleGramBlog.App.ViewModels;
namespace TeleGramBlog.Tests;
public class MigrationTests
{
    [Fact] public async Task MobileSnapshotUsesDesktopPageMemoryAndLoadedHistorySearch()
    {
        using var vm = new MainViewModel();
        await vm.PreviewAsync(); await vm.SelectGroupAsync(vm.Groups[0]);
        var firstTopic = vm.Topics[0]; await vm.SelectTopicAsync(firstTopic);
        await vm.NextAsync(); Assert.Equal(2, vm.CurrentPage);
        var firstId = vm.Cards[0].Post.Id;
        vm.PostQuery = "Coastal";
        Assert.Equal(9, vm.Cards.Count); // All 54 loaded posts, not only current page.
        vm.BackToOverview(); await vm.SelectTopicAsync(vm.Topics[1]);
        await vm.SelectTopicAsync(firstTopic);
        Assert.Equal("Coastal", vm.PostQuery); Assert.Equal(2, vm.CurrentPage);
        vm.PostQuery = ""; Assert.Equal(firstId, vm.Cards[0].Post.Id);
        Assert.Equal(24, vm.PageSize); Assert.True(vm.KnownPages >= 2);
    }
}
