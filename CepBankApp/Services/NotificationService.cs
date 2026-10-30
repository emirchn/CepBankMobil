using System.Globalization;
using CepBankApp.Data;
using CepBankApp.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Microsoft.Maui.Storage;

namespace CepBankApp.Services;

public class NotificationService
{
    private readonly DatabaseService _database;
    private readonly SemaphoreSlim _scheduleGate = new(1, 1);
    private readonly SemaphoreSlim _balanceGate = new(1, 1);
    private static readonly CultureInfo Turkish = new("tr-TR");

    public NotificationService(DatabaseService database) => _database = database;

    public async Task RequestPermissionAsync() =>
        await LocalNotificationCenter.Current.RequestNotificationPermission(new NotificationPermission { AskPermission = true });

    public async Task ScheduleUpcomingPaymentsAsync()
    {
        if (!await LocalNotificationCenter.Current.AreNotificationsEnabled(new NotificationPermission { AskPermission = false })) return;
        await _scheduleGate.WaitAsync();
        try
        {
            await _database.InitializeAsync();
            LocalNotificationCenter.Current.CancelAll();
            var today = DateTime.Today;
            var horizon = today.AddDays(60);
            var planned = new List<(DateTime When, NotificationRequest Request)>();
            var cards = (await _database.GetCreditCardsAsync()).Where(card => card.IsActive).ToList();
            foreach (var card in cards)
            {
                for (var offset = 0; offset <= 2; offset++)
                {
                    var month = new DateTime(today.Year, today.Month, 1).AddMonths(offset);
                    var statement = RecurringSchedule.MonthlyDate(month.Year, month.Month, card.StatementDay);
                    var payment = RecurringSchedule.MonthlyDate(month.Year, month.Month, card.PaymentDay);
                    foreach (var days in NotificationSettings.ReminderDays)
                    {
                        if (NotificationSettings.HasDay(NotificationSettings.StatementDays, days))
                            AddSchedule(planned, $"statement:{card.Id}:{statement:yyyyMMdd}:{days}",
                                "Kredi kartı hesap kesimi", $"{card.Name} kartının hesap kesimine {days} gün kaldı.", statement.AddDays(-days), horizon);
                        if (NotificationSettings.HasDay(NotificationSettings.PaymentDays, days))
                            AddSchedule(planned, $"payment:{card.Id}:{payment:yyyyMMdd}:{days}",
                                "Kredi kartı son ödeme", $"{card.Name} kartının son ödemesine {days} gün kaldı.", payment.AddDays(-days), horizon);
                    }
                }
            }

            var recurringItems = (await _database.GetRecurringItemsAsync()).Where(item => item.IsActive).ToList();
            foreach (var item in recurringItems)
            {
                for (var offset = 0; offset <= 2; offset++)
                {
                    var month = new DateTime(today.Year, today.Month, 1).AddMonths(offset);
                    var due = RecurringSchedule.MonthlyDate(month.Year, month.Month, item.DayOfMonth);
                    if (due < item.StartDate.Date || due > (item.EndDate?.Date ?? item.StartDate.Date.AddYears(1))) continue;
                    AddSchedule(planned, $"recurring:{item.Id}:{due:yyyyMMdd}",
                        item.TransactionType == TransactionType.Income ? "Düzenli gelir günü" : "Düzenli ödeme günü",
                        $"{item.Name}: {item.Amount.ToString("N2", Turkish)} TL", due, horizon);
                    if (item.TransactionType != TransactionType.Expense || item.Category != "Kredi Ödemesi") continue;
                    foreach (var days in NotificationSettings.ReminderDays.Where(day => NotificationSettings.HasDay(item.LoanReminderDaysMask, day)))
                        AddSchedule(planned, $"loan:{item.Id}:{due:yyyyMMdd}:{days}", "Yaklaşan kredi ödemesi",
                            $"{item.Name} ödemesine {days} gün kaldı: {item.Amount.ToString("N2", Turkish)} TL", due.AddDays(-days), horizon);
                }
            }
            foreach (var reminder in planned.OrderBy(item => item.When).Take(60))
                await LocalNotificationCenter.Current.Show(reminder.Request);
        }
        finally { _scheduleGate.Release(); }
    }

    public async Task CheckMonthlyBalanceAsync()
    {
        await _balanceGate.WaitAsync();
        try
        {
            if (!await LocalNotificationCenter.Current.AreNotificationsEnabled(new NotificationPermission { AskPermission = false })) return;
            await _database.InitializeAsync();
            var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var transactions = await _database.GetTransactionsForMonthAsync(month);
            var income = transactions.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount);
            if (income <= 0) return;
            var expense = transactions.Where(item => item.Type == TransactionType.Expense && item.CardDebtChange <= 0).Sum(item => item.Amount);
            var cardDebt = (await _database.GetCreditCardsAsync()).Where(card => card.IsActive).Sum(card => card.CurrentDebt);
            var spent = expense + cardDebt;
            var remaining = income - spent;
            foreach (var percent in NotificationSettings.BalancePercents)
            {
                if (!NotificationSettings.HasThreshold(NotificationSettings.BalanceThresholds, percent) || spent < income * percent / 100) continue;
                var marker = $"notify_balance_sent_{month:yyyyMM}_{percent}";
                if (Preferences.Default.Get(marker, false)) continue;
                await LocalNotificationCenter.Current.Show(new NotificationRequest
                {
                    NotificationId = StableId($"balance:{month:yyyyMM}:{percent}"),
                    Title = "Aylık durum",
                    Description = $"Aylık gelirinizin %{percent}'ini harcadınız, kalan bakiye: {remaining.ToString("N2", Turkish)} TL"
                });
                Preferences.Default.Set(marker, true);
            }
        }
        finally { _balanceGate.Release(); }
    }

    public static void ResetCurrentBalanceAlerts()
    {
        var month = DateTime.Today.ToString("yyyyMM", CultureInfo.InvariantCulture);
        foreach (var percent in NotificationSettings.BalancePercents)
            Preferences.Default.Remove($"notify_balance_sent_{month}_{percent}");
    }

    private static void AddSchedule(List<(DateTime When, NotificationRequest Request)> planned,
        string key, string title, string description, DateTime day, DateTime horizon)
    {
        if (day.Date < DateTime.Today || day.Date > horizon) return;
        var notifyAt = day.Date.AddHours(9);
        if (notifyAt <= DateTime.Now) notifyAt = DateTime.Now.AddSeconds(5);
        planned.Add((notifyAt, new NotificationRequest
        {
            NotificationId = StableId(key), Title = title, Description = description,
            Schedule = new NotificationRequestSchedule { NotifyTime = notifyAt }
        }));
    }

    private static int StableId(string key)
    {
        uint hash = 2166136261;
        foreach (var character in key) hash = unchecked((hash ^ character) * 16777619);
        return (int)(hash & 0x3fffffff) + 1;
    }
}
