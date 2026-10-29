namespace CepBankApp.Services;

public static class AppAppearance
{
    public static AppTheme Resolve(string mode) => mode switch
    {
        "light" => AppTheme.Light,
        "dark" => AppTheme.Dark,
        _ => AppTheme.Unspecified
    };

    public static Task ApplyAsync(string mode) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        if (Application.Current is { } app)
            app.UserAppTheme = Resolve(mode);
    });
}
