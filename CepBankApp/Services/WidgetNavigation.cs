namespace CepBankApp.Services;

public static class WidgetNavigation
{
    private static readonly Dictionary<string, string> Routes = new(StringComparer.Ordinal)
    {
        ["/credit-cards"] = "transaction/cards",
        ["/cards"] = "cards",
        ["/accounts"] = "accounts",
        ["/expenses"] = "expenses",
        ["/reminders"] = "notes/reminders",
        ["/meal-card"] = "cards?type=meal",
        ["/fuel-card"] = "cards?type=fuel",
        ["/reminder/new"] = "notes/reminders/new",
        ["/shopping/new"] = "notes/shopping/new",
        ["/place/new"] = "notes/places/new"
    };

    private static string? _pendingRoute;

    public static event Action? RouteRequested;

    public static bool TryOpen(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            uri.Scheme != "ecappscepbank" || uri.Host != "widget" ||
            !Routes.TryGetValue(uri.AbsolutePath, out var route))
            return false;

        _pendingRoute = route;
        RouteRequested?.Invoke();
        return true;
    }

    public static string? TakePendingRoute()
    {
        var route = _pendingRoute;
        _pendingRoute = null;
        return route;
    }
}
