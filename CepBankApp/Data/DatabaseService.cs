using SQLite;
using CepBankApp.Models;

namespace CepBankApp.Data;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;

    public DatabaseService()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "cepbank.db3");
        _database = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync()
    {
        await _database.CreateTableAsync<FinancialTransaction>();
        await _database.CreateTableAsync<RecurringItem>();
        await _database.CreateTableAsync<CreditCard>();
        await _database.CreateTableAsync<CardStatement>();
    }

    public Task<List<FinancialTransaction>> GetTransactionsAsync()
    {
        return _database.Table<FinancialTransaction>()
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .ToListAsync();
    }

    public Task AddTransactionAsync(FinancialTransaction transaction) =>
        _database.RunInTransactionAsync(connection =>
        {
            if (transaction.CardId is int cardId)
            {
                var card = connection.Find<CreditCard>(cardId);
                if (card is null || !card.IsActive)
                    throw new InvalidOperationException("Seçilen kart bulunamadı.");
                if (transaction.Type != TransactionType.Expense)
                    throw new InvalidOperationException("Kart yalnızca gider işlemlerinde seçilebilir.");

                var isPayment = transaction.Category == "Kredi Kartı Ödemesi";
                var change = isPayment ? -transaction.Amount : transaction.Amount;
                if (card.CurrentDebt + change < 0)
                    throw new InvalidOperationException("Ödeme tutarı kartın mevcut borcunu aşamaz.");

                transaction.CardDebtChange = change;
                transaction.CardStatementDate = isPayment ? null : GetCardStatementDate(transaction.Date, card.StatementDay);
                card.CurrentDebt += change;
                connection.Update(card);
            }
            connection.Insert(transaction);
        });

    public Task DeleteTransactionAsync(FinancialTransaction transaction) =>
        _database.RunInTransactionAsync(connection =>
        {
            if (transaction.CardId is int cardId && transaction.CardDebtChange != 0)
            {
                var card = connection.Find<CreditCard>(cardId);
                if (card is not null)
                {
                    card.CurrentDebt = Math.Max(0, card.CurrentDebt - transaction.CardDebtChange);
                    connection.Update(card);
                }
            }
            connection.Delete(transaction);
        });

    public Task<List<RecurringItem>> GetRecurringItemsAsync() => _database.Table<RecurringItem>().OrderBy(item => item.DayOfMonth).ToListAsync();

    public Task<int> AddRecurringItemAsync(RecurringItem item) => _database.InsertAsync(item);

    public Task<int> UpdateRecurringItemAsync(RecurringItem item) => _database.UpdateAsync(item);

    public Task<int> DeleteRecurringItemAsync(RecurringItem item) => _database.DeleteAsync(item);

    public Task<List<CreditCard>> GetCreditCardsAsync() => _database.Table<CreditCard>().OrderBy(card => card.Name).ToListAsync();

    public Task<int> AddCreditCardAsync(CreditCard card) => _database.InsertAsync(card);

    public Task<int> UpdateCreditCardAsync(CreditCard card) => _database.UpdateAsync(card);

    public Task<int> UpdateCreditCardDetailsAsync(CreditCard card) =>
        _database.ExecuteAsync(
            "UPDATE CreditCard SET Name = ?, LastFourDigits = ?, CardholderName = ?, ExpirationMonth = ?, ExpirationYear = ?, SecureDetailsKey = ?, StatementDay = ?, PaymentDay = ?, CreditLimit = ? WHERE Id = ?",
            card.Name, card.LastFourDigits, card.CardholderName, card.ExpirationMonth, card.ExpirationYear,
            card.SecureDetailsKey, card.StatementDay, card.PaymentDay, card.CreditLimit, card.Id);

    public static DateTime GetCardStatementDate(DateTime transactionDate, int statementDay)
    {
        return CepBankApp.Services.CardBilling.StatementDate(transactionDate, statementDay);
    }

    public Task ArchiveCreditCardAsync(CreditCard card)
    {
        card.IsActive = false;
        return _database.RunInTransactionAsync(connection =>
        {
            connection.Update(card);
            connection.Execute("UPDATE RecurringItem SET CardId = NULL WHERE CardId = ?", card.Id);
        });
    }

    public Task<List<CardStatement>> GetStatementsAsync() => _database.Table<CardStatement>()
        .OrderBy(statement => statement.PaymentDate)
        .ToListAsync();

    public async Task<CardStatement?> GetStatementAsync(int cardId, DateTime statementDate)
    {
        var statement = await _database.Table<CardStatement>()
            .Where(item => item.CardId == cardId && item.StatementDate == statementDate)
            .FirstOrDefaultAsync();
        return statement;
    }

    public Task<int> AddStatementAsync(CardStatement statement) => _database.InsertAsync(statement);

    public Task<int> UpdateStatementAsync(CardStatement statement) => _database.UpdateAsync(statement);

    public Task<List<CardStatement>> GetUpcomingStatementsAsync(DateTime from, DateTime to) =>
        _database.Table<CardStatement>()
            .Where(statement => !statement.IsPaid && statement.PaymentDate >= from && statement.PaymentDate <= to)
            .OrderBy(statement => statement.PaymentDate)
            .ToListAsync();

    public async Task<(decimal Income, decimal Expense)> GetMonthlyTotalsAsync(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        var transactions = await _database.Table<FinancialTransaction>()
            .Where(transaction => transaction.Date >= start && transaction.Date < end)
            .ToListAsync();

        return (
            transactions.Where(transaction => transaction.Type == TransactionType.Income).Sum(transaction => transaction.Amount),
            transactions.Where(transaction => transaction.Type == TransactionType.Expense).Sum(transaction => transaction.Amount));
    }

    public Task ClearAllDataAsync() =>
        _database.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<CardStatement>();
            connection.DeleteAll<FinancialTransaction>();
            connection.DeleteAll<RecurringItem>();
            connection.DeleteAll<CreditCard>();
        });

    public async Task ExportJsonAsync(string destinationPath)
    {
        var data = new
        {
            Transactions = await GetTransactionsAsync(),
            RecurringItems = await GetRecurringItemsAsync(),
            CreditCards = await GetCreditCardsAsync(),
            Statements = await GetStatementsAsync(),
            ExportedAt = DateTime.Now
        };

        await File.WriteAllTextAsync(destinationPath, System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
