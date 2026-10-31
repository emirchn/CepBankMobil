using CepBankApp.Models;

namespace CepBankApp.Services;

public static class RecurringSchedule
{
    public static DateTime MonthlyDate(int year, int month, int day) =>
        new(year, month, Math.Clamp(day, 1, DateTime.DaysInMonth(year, month)));

    public static List<FinancialTransaction> ProjectMonth(
        IEnumerable<RecurringItem> items, IEnumerable<FinancialTransaction> existing, DateTime month, DateTime today)
    {
        var projections = new List<FinancialTransaction>();
        foreach (var item in items.Where(item => item.IsActive))
        {
            var date = MonthlyDate(month.Year, month.Month, item.DayOfMonth);
            var end = item.EndDate?.Date ?? DateTime.MaxValue.Date;
            var source = $"Recurring:{item.Id}";
            if (date <= today.Date || date < item.StartDate.Date || date > end ||
                existing.Any(transaction => transaction.Source == source && transaction.Date.Year == date.Year && transaction.Date.Month == date.Month))
                continue;

            if (item.RecurringType == RecurringType.Loan && item.RemainingInstallments > 0)
            {
                var earlierPayments = 0;
                var cursor = item.StartDate.Date > today.Date ? item.StartDate.Date : today.Date;
                cursor = new DateTime(cursor.Year, cursor.Month, 1);
                while (cursor < new DateTime(date.Year, date.Month, 1))
                {
                    var previous = MonthlyDate(cursor.Year, cursor.Month, item.DayOfMonth);
                    if (previous > today.Date && previous >= item.StartDate.Date && previous <= end)
                        earlierPayments++;
                    cursor = cursor.AddMonths(1);
                }
                if (earlierPayments >= item.RemainingInstallments) continue;
            }

            projections.Add(new FinancialTransaction
            {
                Date = date,
                Type = item.TransactionType,
                Amount = item.Amount,
                Description = item.Name,
                Category = item.Category,
                CardId = item.TransactionType == TransactionType.Expense ? item.CardId : null,
                Source = source,
                IsRecurring = true,
                IsProjected = true
            });
        }
        return projections;
    }
}
