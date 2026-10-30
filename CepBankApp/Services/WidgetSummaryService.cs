using System.Globalization;
using System.Text.Json;
using CepBankApp.Data;
using CepBankApp.Models;

namespace CepBankApp.Services;

public sealed class WidgetSummaryService(DatabaseService database, RecurringTransactionService recurring)
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task TryRefreshAsync()
    {
        try { await RefreshAsync(); }
        catch { /* Financial changes remain saved when the widget is unavailable. */ }
    }

    public async Task RefreshAsync()
    {
#if IOS
        var group = Foundation.NSFileManager.DefaultManager.GetContainerUrl("group.com.emir.cepbankapp.cepbankapp");
        if (group?.Path is not string directory) return;

        await _writeGate.WaitAsync();
        try
        {
            var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var transactions = await recurring.GetMonthTransactionsAsync(month);
            var cardDebt = (await database.GetCreditCardsAsync()).Where(card => card.IsActive).Sum(card => card.CurrentDebt);
            var income = transactions.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount);
            var expense = transactions.Where(item => item.Type == TransactionType.Expense && item.CardDebtChange <= 0)
                .Sum(item => item.Amount);
            var totalExpense = expense + cardDebt;
            var snapshot = new
            {
                month = month.ToString("MMMM yyyy", new CultureInfo("tr-TR")),
                monthKey = month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                income,
                expense = totalExpense,
                cardDebt,
                net = income - totalExpense,
                updatedAt = DateTimeOffset.Now.ToString("O")
            };
            var path = Path.Combine(directory, "widget-summary.json");
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot));
            File.Move(temporary, path, true);
        }
        finally { _writeGate.Release(); }
#else
        await Task.CompletedTask;
#endif
    }
}
