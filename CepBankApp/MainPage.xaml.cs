namespace CepBankApp;

public partial class MainPage : ContentPage
{
	private bool _notesSurfaceActive;
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
		if (_notesSurfaceActive) return;
		var dark = mode == "dark" || (mode == "device" && requestedTheme == AppTheme.Dark);
		BackgroundColor = Color.FromArgb(mode is "glassy" or "custom" ? "#031C26" : dark ? "#161D38" : "#EEF2FF");
	}

	public static Task SetNotesBackgroundAsync(string mode, bool dark) => MainThread.InvokeOnMainThreadAsync(() =>
	{
		if (Application.Current?.Windows.FirstOrDefault()?.Page is not MainPage page) return;
		page._notesSurfaceActive = true;
		page.BackgroundColor = Color.FromArgb(mode == "forest" ? "#051F20" : mode == "violet" ? "#0B0720" : mode == "glassy" ? "#100C20" : dark ? "#11161C" : "#F7F8FA");
		if (Application.Current is { } app)
		{
			var desiredTheme = mode == "device" ? AppTheme.Unspecified : dark ? AppTheme.Dark : AppTheme.Light;
			if (app.UserAppTheme != desiredTheme) app.UserAppTheme = desiredTheme;
		}
	});

	public static Task RestoreFinanceBackgroundAsync() => MainThread.InvokeOnMainThreadAsync(() =>
	{
		if (Application.Current?.Windows.FirstOrDefault()?.Page is not MainPage page) return;
		page._notesSurfaceActive = false;
		if (Application.Current is { } app)
			app.UserAppTheme = Services.AppAppearance.Resolve(Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device"));
		page.UpdatePageBackground(Microsoft.Maui.Storage.Preferences.Default.Get("theme_mode", "device"),
			Application.Current?.RequestedTheme ?? AppTheme.Light);
	});

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
