namespace CepBankApp.Services;

public static class AppAppearance
{
    public static AppTheme Resolve(string mode) => mode switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        "glassy" => AppTheme.Light,
        _ => AppTheme.Unspecified
    };

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
