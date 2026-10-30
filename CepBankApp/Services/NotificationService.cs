using System.Globalization;
using CepBankApp.Data;
using CepBankApp.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace CepBankApp.Services;

public class NotificationService
{
    private readonly DatabaseService _database;
    private readonly SemaphoreSlim _scheduleGate = new(1, 1);
    private readonly SemaphoreSlim _balanceGate = new(1, 1);
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly int DailyBalanceId = StableId("balance:daily");

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
            var today = DateTime.Today;
            var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
            var oldReminderIds = pending.Select(item => item.NotificationId)
                .Where(id => id != DailyBalanceId).ToArray();
            if (oldReminderIds.Length > 0) LocalNotificationCenter.Current.Cancel(oldReminderIds);
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
                    AddSchedule(planned, $"statement:{card.Id}:{statement:yyyyMMdd}:tomorrow",
                        "Yarın hesap kesimi", $"Yarın {card.Name} kartınızın hesap kesimi var.", statement.AddDays(-1), horizon);
                    AddSchedule(planned, $"statement:{card.Id}:{statement:yyyyMMdd}:today",
                        "Hesap kesimi bugün", $"Bugün {card.Name} kartınızın hesap kesim günü.", statement, horizon);
                    AddSchedule(planned, $"payment:{card.Id}:{payment:yyyyMMdd}:tomorrow",
                        "Yarın son ödeme günü", $"Yarın {card.Name} kartınızın son ödeme günü.", payment.AddDays(-1), horizon);
                    AddSchedule(planned, $"payment:{card.Id}:{payment:yyyyMMdd}:today",
                        "Son ödeme günü bugün", $"Bugün {card.Name} kartınızın son ödeme günü.", payment, horizon);
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

            var recordedToday = (await _database.GetTransactionsForMonthAsync(new DateTime(today.Year, today.Month, 1)))
                .Where(item => item.Date.Date == today).Select(item => item.Source).ToHashSet();
            var recurringItems = (await _database.GetRecurringItemsAsync())
                .Where(item => item.IsActive ||
                    (item.TransactionType == TransactionType.Expense && item.Category == "Kredi Ödemesi" &&
                     recordedToday.Contains($"Recurring:{item.Id}"))).ToList();
            foreach (var item in recurringItems)
            {
                for (var offset = 0; offset <= 2; offset++)
                {
                    var month = new DateTime(today.Year, today.Month, 1).AddMonths(offset);
                    var due = RecurringSchedule.MonthlyDate(month.Year, month.Month, item.DayOfMonth);
                    if (!item.IsActive && due.Date != today) continue;
                    if (due < item.StartDate.Date || due > (item.EndDate?.Date ?? item.StartDate.Date.AddYears(1))) continue;
                    var isLoan = item.TransactionType == TransactionType.Expense && item.Category == "Kredi Ödemesi";
                    if (!isLoan)
                        AddSchedule(planned, $"recurring:{item.Id}:{due:yyyyMMdd}",
                            item.TransactionType == TransactionType.Income ? "Düzenli gelir günü" : "Düzenli ödeme günü",
                            $"{item.Name}: {item.Amount.ToString("N2", Turkish)} TL", due, horizon);
                    if (!isLoan) continue;
                    AddSchedule(planned, $"loan:{item.Id}:{due:yyyyMMdd}:tomorrow",
                        "Yarın kredi ödemesi", $"Yarın {item.Name} kredinizin ödeme günü.", due.AddDays(-1), horizon);
                    AddSchedule(planned, $"loan:{item.Id}:{due:yyyyMMdd}:today",
                        "Kredi ödemesi bugün", $"Bugün {item.Name} kredinizin ödeme günü.", due, horizon);
                    foreach (var days in NotificationSettings.ReminderDays.Where(day => NotificationSettings.HasDay(item.LoanReminderDaysMask, day)))
                        AddSchedule(planned, $"loan:{item.Id}:{due:yyyyMMdd}:{days}", "Yaklaşan kredi ödemesi",
                            $"{item.Name} ödemesine {days} gün kaldı: {item.Amount.ToString("N2", Turkish)} TL", due.AddDays(-days), horizon);
                }
            }
            foreach (var reminder in planned.OrderBy(item => item.When).Take(58))
                await LocalNotificationCenter.Current.Show(reminder.Request);
        }
        finally { _scheduleGate.Release(); }
    }

    public async Task ScheduleDailyBalanceAsync()
    {
        await _balanceGate.WaitAsync();
        try
        {
            var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
            var oldBalanceIds = new[] { DateTime.Today.AddMonths(-1), DateTime.Today }
                .SelectMany(month => new[] { 50, 75, 90 }
                    .Select(percent => StableId($"balance:{month:yyyyMM}:{percent}")))
                .Append(DailyBalanceId)
                .Where(id => pending.Any(item => item.NotificationId == id))
                .ToArray();
            if (oldBalanceIds.Length > 0) LocalNotificationCenter.Current.Cancel(oldBalanceIds);
            if (!NotificationSettings.DailyBalanceEnabled ||
                !await LocalNotificationCenter.Current.AreNotificationsEnabled(new NotificationPermission { AskPermission = false })) return;
            await _database.InitializeAsync();
            var account = await _database.GetHomeBankAccountAsync();
            if (account is null) return;
            await LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = DailyBalanceId,
                Title = "Günlük bakiye",
                Description = $"{account.Name} güncel bakiyesi: {account.CurrentBalance.ToString("N2", Turkish)} TL",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = NextDeliveryTime(),
                    RepeatType = NotificationRepeat.Daily
                }
            });
        }
        finally { _balanceGate.Release(); }
    }

    private static void AddSchedule(List<(DateTime When, NotificationRequest Request)> planned,
        string key, string title, string description, DateTime day, DateTime horizon)
    {
        if (day.Date < DateTime.Today || day.Date > horizon) return;
        var notifyAt = day.Date.AddHours(16);
        if (notifyAt <= DateTime.Now) return;
        planned.Add((notifyAt, new NotificationRequest
        {
            NotificationId = StableId(key), Title = title, Description = description,
            Schedule = new NotificationRequestSchedule { NotifyTime = notifyAt }
        }));
    }

    private static DateTime NextDeliveryTime()
    {
        var atFour = DateTime.Today.AddHours(16);
        return atFour > DateTime.Now ? atFour : atFour.AddDays(1);
    }

    private static int StableId(string key)
    {
        uint hash = 2166136261;
        foreach (var character in key) hash = unchecked((hash ^ character) * 16777619);
        return (int)(hash & 0x3fffffff) + 1;
    }
}
