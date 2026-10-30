using Microsoft.Maui.Storage;

namespace CepBankApp.Services;

public static class NotificationSettings
{
    public static readonly int[] ReminderDays = [3, 5, 7];
    public static readonly int[] BalancePercents = [50, 75, 90];

    public static int StatementDays
    {
        get => Preferences.Default.Get("notify_statement_days", 1 << 3);
        set => Preferences.Default.Set("notify_statement_days", value);
    }

    public static int PaymentDays
    {
        get => Preferences.Default.Get("notify_payment_days", 1 << 3);
        set => Preferences.Default.Set("notify_payment_days", value);
    }

    public static int BalanceThresholds
    {
        get => Preferences.Default.Get("notify_balance_thresholds", 1 << 1);
        set => Preferences.Default.Set("notify_balance_thresholds", value);
    }

    public static bool HasDay(int mask, int day) => (mask & (1 << day)) != 0;
    public static int ToggleDay(int mask, int day) => mask ^ (1 << day);
    public static bool HasThreshold(int mask, int percent) => (mask & (1 << Array.IndexOf(BalancePercents, percent))) != 0;
    public static int ToggleThreshold(int mask, int percent) => mask ^ (1 << Array.IndexOf(BalancePercents, percent));
}
