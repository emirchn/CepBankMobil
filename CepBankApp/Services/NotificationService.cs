using CepBankApp.Data;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace CepBankApp.Services;

public class NotificationService
{
    private readonly DatabaseService _database;

    public NotificationService(DatabaseService database)
    {
        _database = database;
    }

    public async Task RequestPermissionAsync()
    {
        await LocalNotificationCenter.Current.RequestNotificationPermission(new NotificationPermission
        {
            AskPermission = true
        });
    }

    public async Task ScheduleUpcomingPaymentsAsync()
    {
        await _database.InitializeAsync();
        var today = DateTime.Today;
        var end = today.AddDays(7);
        var recurringItems = await _database.GetRecurringItemsAsync();
        var cards = await _database.GetCreditCardsAsync();

        foreach (var item in recurringItems.Where(item => item.IsActive))
        {
            var date = GetNextMonthlyDate(today, item.DayOfMonth);
            if (date >= today && date <= end)
            {
                await LocalNotificationCenter.Current.Show(new NotificationRequest
                {
                    NotificationId = 100000 + item.Id,
                    Title = "Yaklaşan ödeme",
                    Description = $"{item.Name}: {item.Amount:N2} TL",
                    Schedule = new NotificationRequestSchedule { NotifyTime = date.AddHours(9) }
                });
            }
        }

        var statements = await _database.GetUpcomingStatementsAsync(today, end);
        foreach (var statement in statements)
        {
            var card = cards.FirstOrDefault(card => card.Id == statement.CardId);
            await LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = 200000 + statement.Id,
                Title = "Kart son ödeme tarihi",
                Description = $"{card?.Name ?? "Kredi kartı"}: {statement.Amount:N2} TL",
                Schedule = new NotificationRequestSchedule { NotifyTime = statement.PaymentDate.AddDays(-1).AddHours(9) }
            });
        }
    }

    private static DateTime GetNextMonthlyDate(DateTime today, int requestedDay)
    {
        var current = GetMonthlyDate(today.Year, today.Month, requestedDay);
        return current < today ? GetMonthlyDate(today.Year, today.Month + 1, requestedDay) : current;
    }

    private static DateTime GetMonthlyDate(int year, int month, int requestedDay)
    {
        if (month == 13) { year++; month = 1; }
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(requestedDay, lastDay));
    }
}
