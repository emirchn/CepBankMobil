namespace CepBankApp.Services;

public sealed class NotesAppearance
{
    public event Action? Changed;
    public NotesAppearance()
    {
        if (Application.Current is { } app)
            app.RequestedThemeChanged += (_, _) => { if (Mode == "device") Changed?.Invoke(); };
    }
    public string Mode
    {
        get => Preferences.Default.Get("notes_theme_mode", "device");
        set
        {
            Preferences.Default.Set("notes_theme_mode", value);
            PrepareForNotes();
            Changed?.Invoke();
        }
    }

    public bool IsDark => Mode == "dark" || (Mode == "device" && DeviceThemeIsDark());

    public void PrepareForNotes()
    {
        if (Mode == "device" && Application.Current is { } app)
            app.UserAppTheme = AppTheme.Unspecified;
    }

    private static bool DeviceThemeIsDark()
    {
        if (Application.Current?.RequestedTheme is AppTheme.Dark) return true;
        if (Application.Current?.RequestedTheme is AppTheme.Light) return false;
#if IOS
        return UIKit.UIScreen.MainScreen.TraitCollection.UserInterfaceStyle == UIKit.UIUserInterfaceStyle.Dark;
#else
        return false;
#endif
    }
}
