namespace TeleGramBlog.App;

public partial class App : Application
{
    public App()
    {
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(Luma.Services.RuntimePlatform.DataRoot, "WebView2"));
        InitializeComponent();
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        UserAppTheme = AppTheme.Light;
        var args = Environment.GetCommandLineArgs();
        var legacy = args.Any(a => a is "--legacy-ui" or "--capture-shell" or "--capture-viewer" or "--memory-probe" or "--capture-cards");
        if(legacy)
        {
            var oldPage = new MainPage();
            var oldWindow = new Window(new NavigationPage(oldPage)) {Title="TeleBlog · Legacy diagnostics",Width=1440,Height=940,MinimumWidth=880,MinimumHeight=640};
            oldWindow.Destroying += (_,_)=>oldPage.Dispose();
            return oldWindow;
        }
        var page = new Luma.MainPage();
        var window = new Window(page) {Title="TeleBlog",Width=1440,Height=940,MinimumWidth=880,MinimumHeight=640};
        window.Stopped += (_,_)=>page.Suspend();
        window.Resumed += (_,_)=>page.Resume();
        window.Destroying += (_,_)=>page.Dispose();
        return window;
    }
}
