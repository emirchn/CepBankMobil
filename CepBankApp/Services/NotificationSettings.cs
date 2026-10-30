using Microsoft.Maui.Storage;

namespace CepBankApp.Services;

public static class NotificationSettings
{
    public static readonly int[] ReminderDays = [3, 5, 7];

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

    public static bool DailyBalanceEnabled
    {
        get => Preferences.Default.Get("notify_daily_balance_enabled", true);
        set => Preferences.Default.Set("notify_daily_balance_enabled", value);
    }

    public static bool HasDay(int mask, int day) => (mask & (1 << day)) != 0;
    public static int ToggleDay(int mask, int day) => mask ^ (1 << day);
}
