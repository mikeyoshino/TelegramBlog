namespace Luma;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => MauiApp.CreateBuilder().UseMauiApp<App>().Build();
}
public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme = AppTheme.Light;
        var page = new MainPage();
        var window = new Window(page) { Title = "TeleBlog" };
        window.Stopped += (_, _) => page.Suspend();
        window.Resumed += (_, _) => page.Resume();
        window.Destroying += (_, _) => page.Dispose();
        return window;
    }
}
