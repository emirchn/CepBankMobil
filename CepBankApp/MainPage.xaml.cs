namespace CepBankApp;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		UpdatePageBackground(Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device"), Application.Current?.RequestedTheme ?? AppTheme.Light);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (!NativeStartup.IsVisible) return;
		_ = HideAfterTimeoutAsync();
	}

	private async Task HideAfterTimeoutAsync()
	{
		await Task.Delay(22000);
		await CompleteStartupAsync();
	}

	public static Task CompleteStartupAsync() => MainThread.InvokeOnMainThreadAsync(() =>
	{
		if (Application.Current?.Windows.FirstOrDefault()?.Page is not MainPage page || !page.NativeStartup.IsVisible) return;
		page.NativeStartup.IsVisible = false;
	});

	public void UpdatePageBackground(string mode, AppTheme requestedTheme)
	{
		var dark = mode == "dark" || (mode == "device" && requestedTheme == AppTheme.Dark);
		BackgroundColor = Color.FromArgb(mode is "glassy" or "custom" ? "#C1CCF5" : dark ? "#161D38" : "#EEF2FF");
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
