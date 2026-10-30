namespace CepBankApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = Services.AppAppearance.Resolve(Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device"));
		RequestedThemeChanged += (_, args) =>
		{
			if (Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device") != "device") return;
			foreach (var window in Windows)
				if (window.Page is MainPage page)
					page.UpdatePageBackground("device", args.RequestedTheme);
		};
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "CepBank" };
	}
}
