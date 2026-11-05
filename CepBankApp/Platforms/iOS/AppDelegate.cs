using Foundation;
using UIKit;
using CepBankApp.Services;

namespace CepBankApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		return WidgetNavigation.TryOpen(url.AbsoluteString) || base.OpenUrl(application, url, options);
	}
}
