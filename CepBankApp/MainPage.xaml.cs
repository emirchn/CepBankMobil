namespace CepBankApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private void OnBlazorWebViewInitialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs args)
	{
#if IOS
		args.WebView.Opaque = false;
		args.WebView.BackgroundColor = UIKit.UIColor.Clear;
		args.WebView.ScrollView.BackgroundColor = UIKit.UIColor.Clear;
		args.WebView.ScrollView.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
#endif
	}
}
