using CepBankApp.Data;
using CepBankApp.Models;

namespace CepBankApp.Services;

public class RecurringTransactionService
{
    private readonly DatabaseService _database;
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private DateTime? _lastProcessedDate;

    public RecurringTransactionService(DatabaseService database)
    {
        _database = database;
    }

    public async Task ProcessAsync(bool force = false)
    {
        await _processingGate.WaitAsync();
        try
        {
            if (!force && _lastProcessedDate == DateTime.Today) return;
            await ProcessDueTransactionsAsync();
            _lastProcessedDate = DateTime.Today;
        }
        finally { _processingGate.Release(); }
    }

    private async Task ProcessDueTransactionsAsync()
    {
        await _database.InitializeAsync();
        var items = await _database.GetRecurringItemsAsync();
        var transactions = await _database.GetTransactionsAsync();
        var existingMonths = transactions.Where(transaction => transaction.Source.StartsWith("Recurring:"))
            .Select(transaction => (transaction.Source, transaction.Date.Year, transaction.Date.Month)).ToHashSet();
        var today = DateTime.Today;

        foreach (var item in items.Where(item => item.IsActive && item.RecurringType == RecurringType.Subscription && item.CardId.HasValue))
        {
            if (item.EndDate.HasValue && item.EndDate.Value.Date < item.StartDate.Date)
            {
                continue;
            }

            var month = new DateTime(item.StartDate.Year, item.StartDate.Month, 1);
            var lastMonth = new DateTime(today.Year, today.Month, 1);
            var createdCount = 0;

            while (month <= lastMonth)
            {
                var candidate = GetMonthlyDate(month.Year, month.Month, item.DayOfMonth);
                candidate = candidate.Date;

                if (candidate >= item.StartDate.Date && candidate <= today)
                {
                    if (item.EndDate.HasValue && candidate > item.EndDate.Value.Date)
                    {
                        break;
                    }

                    var source = $"Recurring:{item.Id}";
                    var alreadyExists = existingMonths.Contains((source, candidate.Year, candidate.Month));

                    if (!alreadyExists)
                    {
                        if (item.RecurringType == RecurringType.Loan && item.RemainingInstallments > 0 && createdCount >= item.RemainingInstallments)
                            break;
                        await _database.AddTransactionAsync(new FinancialTransaction
                        {
                            Date = candidate,
                            Type = item.TransactionType,
                            Amount = item.Amount,
                            Description = item.Name,
                            Category = string.IsNullOrWhiteSpace(item.Category) ? GetCategory(item.RecurringType) : item.Category,
                            CardId = item.TransactionType == TransactionType.Expense ? item.CardId : null,
                            Source = source,
                            IsRecurring = true
                        });
                        createdCount++;
                        existingMonths.Add((source, candidate.Year, candidate.Month));
                    }
                }

                month = month.AddMonths(1);
            }

            if (item.RecurringType == RecurringType.Loan && item.RemainingInstallments > 0)
            {
                var passedInstallments = Enumerable.Range(0, createdCount).Count();
                var remaining = item.RemainingInstallments - passedInstallments;

                if (remaining != item.RemainingInstallments)
                {
                    item.RemainingInstallments = Math.Max(0, remaining);
                    if (item.RemainingInstallments == 0)
                    {
                        item.IsActive = false;
                    }
                    await _database.UpdateRecurringItemAsync(item);
                }
            }
        }
    }

    public async Task<List<FinancialTransaction>> GetProjectedTransactionsAsync(DateTime month, IEnumerable<FinancialTransaction> existing)
    {
        var projected = RecurringSchedule.ProjectMonth((await _database.GetRecurringItemsAsync())
            .Where(item => item.RecurringType == RecurringType.Subscription && item.CardId.HasValue), existing, month, DateTime.Today);
        var cards = await _database.GetCreditCardsAsync();
        foreach (var transaction in projected.Where(transaction => transaction.CardId.HasValue))
        {
            var card = cards.FirstOrDefault(card => card.Id == transaction.CardId);
            if (card is not null)
                transaction.CardStatementDate = CardBilling.StatementDate(transaction.Date, card.StatementDay);
        }
        return projected;
    }

    public async Task<List<FinancialTransaction>> GetMonthTransactionsAsync(DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        var recorded = await _database.GetTransactionsForMonthAsync(start);
        var projected = await GetProjectedTransactionsAsync(start, recorded);
        return recorded.Concat(projected).Where(transaction => transaction.Date >= start && transaction.Date < end)
            .OrderByDescending(transaction => transaction.Date).ThenByDescending(transaction => transaction.Id).ToList();
    }

    private static DateTime GetMonthlyDate(int year, int month, int requestedDay)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateTime(year, month, Math.Min(Math.Max(requestedDay, 1), lastDay));
    }

    private static string GetCategory(RecurringType type) => type switch
    {
        RecurringType.Salary => "Maaş",
        RecurringType.Income => "Gelir",
        RecurringType.Loan => "Kredi",
        RecurringType.Subscription => "Abonelik",
        _ => "Gider"
    };
}
