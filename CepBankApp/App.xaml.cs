namespace CepBankApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = Services.AppAppearance.Resolve(Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device"));
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "CepBank" };
	}
}
