using TeleGramBlog.App.ViewModels;
namespace TeleGramBlog.Tests;
public class ViewModelTests
{
    [Fact] public async Task ExpiredSessionReturnsFromWorkspaceToQrLogin()
    {
        var root=Path.Combine(Path.GetTempPath(),"TeleGramBlogTests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var vm=new MainViewModel(dataRoot:root);await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);
            File.WriteAllText(Path.Combine(root,"account.session"),"expired test session");
            vm.RecoverExpiredSession();
            Assert.True(vm.IsLogin);Assert.False(vm.IsDemo);Assert.False(vm.IsGroupPicker);Assert.False(vm.IsWorkspace);Assert.True(vm.ShowQrPanel);
            Assert.Empty(vm.Cards);Assert.Empty(vm.Groups);Assert.False(File.Exists(Path.Combine(root,"account.session")));
        }
        finally{Directory.Delete(root,true);}
    }
    [Fact] public async Task QrIsDefaultPhoneIsOptionalAndMissingConfigIsSpecific()
    {
        using var vm=new MainViewModel();
        Assert.True(vm.ShowQrPanel);Assert.False(vm.ShowLoginInput);Assert.Null(vm.QrPng);
        await vm.ConnectAsync();Assert.Contains("not been configured",vm.Error);Assert.False(vm.IsBusy);Assert.Null(vm.QrPng);
        vm.SwitchLoginMode();Assert.False(vm.ShowQrPanel);Assert.True(vm.ShowLoginInput);Assert.False(vm.HasError);
        vm.SwitchLoginMode();Assert.True(vm.ShowQrPanel);Assert.False(vm.ShowLoginInput);
    }
    [Fact] public async Task PreviewPagesAndSearchAndLogoutAreIsolated()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);
        Assert.True(vm.IsDemo);Assert.True(vm.IsWorkspace);Assert.Equal(24,vm.Cards.Count);Assert.True(vm.CanNext);
        var first=vm.Cards[0].Post.Id;
        await vm.NextAsync();Assert.NotEqual(first,vm.Cards[0].Post.Id);Assert.True(vm.CanPrevious);
        vm.PostQuery="Coastal";Assert.Equal(9,vm.Cards.Count);Assert.All(vm.Cards,c=>{Assert.Contains("Coastal",c.Title);Assert.True(c.HasImage);});
        vm.PostQuery="";Assert.Equal(24,vm.Cards.Count);
        await vm.LogoutAsync();Assert.True(vm.IsLogin);Assert.False(vm.IsDemo);Assert.Empty(vm.Cards);Assert.Empty(vm.RailGroups);
    }
    [Fact] public async Task ChangingTopicResetsPagingAndChangingSizeRecalculates()
    {
        using var vm=new MainViewModel();await vm.PreviewAsync();await vm.SelectGroupAsync(vm.Groups[0]);await vm.SelectTopicAsync(vm.Topics[1]);await vm.NextAsync();
        await vm.SelectTopicAsync(vm.Topics[0]);Assert.False(vm.CanPrevious);Assert.Equal(24,vm.Cards.Count);
        vm.PageSize=48;await vm.ChangePageAsync(1);Assert.Equal(48,vm.Cards.Count);await vm.NextAsync();Assert.Equal(6,vm.Cards.Count);Assert.False(vm.CanNext);
    }
}
