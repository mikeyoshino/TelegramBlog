using Microsoft.Extensions.DependencyInjection;

namespace TeleGramBlog.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        UserAppTheme = AppTheme.Light;
        var page = new MainPage();
        var window = new Window(new NavigationPage(page) { BarBackgroundColor=Colors.White, BarTextColor=Color.FromArgb("#13243A") })
        {Title="TeleGramBlog",Width=1440,Height=940,MinimumWidth=880,MinimumHeight=640};
        window.Destroying += (_,_)=>page.Dispose();
        return window;
	}
}
