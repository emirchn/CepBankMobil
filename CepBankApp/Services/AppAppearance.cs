namespace CepBankApp.Services;

public static class AppAppearance
{
    public static AppTheme Resolve(string mode) => mode switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        "glassy" => AppTheme.Light,
        "custom" => AppTheme.Light,
        _ => AppTheme.Unspecified
    };

    public static async Task<string> GetWallpaperDataUrlAsync()
    {
        var path = Preferences.Default.Get("custom_wallpaper", "");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var mime = Preferences.Default.Get("custom_wallpaper_mime", "image/jpeg");
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (IOException) { return ""; }
    }

    public static Task ApplyAsync(string mode) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (Application.Current is { } app)
        {
            app.UserAppTheme = Resolve(mode);
            foreach (var window in app.Windows)
                if (window.Page is MainPage page)
                    page.UpdatePageBackground(mode, app.RequestedTheme);
        }
    });
}
