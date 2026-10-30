using SQLite;
using CepBankApp.Models;

namespace CepBankApp.Data;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly object _initializationLock = new();
    private Task? _initializationTask;

    public DatabaseService()
    {
        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "cepbank.db3");
        _database = new SQLiteAsyncConnection(databasePath);
    }

    public Task InitializeAsync()
    {
        lock (_initializationLock)
            return _initializationTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        await _database.CreateTableAsync<FinancialTransaction>();
        await _database.CreateTableAsync<RecurringItem>();
        await _database.CreateTableAsync<CreditCard>();
        await _database.CreateTableAsync<CardStatement>();
        await _database.CreateTableAsync<BenefitCard>();
        await _database.CreateTableAsync<BenefitCardMovement>();

        try { await _database.ExecuteAsync("ALTER TABLE RecurringItem ADD COLUMN LoanReminderDaysMask INTEGER NOT NULL DEFAULT 0"); }
        catch (SQLiteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }

        foreach (var statement in new[]
        {
            "ALTER TABLE CreditCard ADD COLUMN IconColor TEXT",
            "ALTER TABLE CreditCard ADD COLUMN RevealSensitive INTEGER"
        })
        {
            try { await _database.ExecuteAsync(statement); }
            catch { }
        }
    }

    public Task<List<FinancialTransaction>> GetTransactionsAsync()
    {
        return _database.Table<FinancialTransaction>()
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.Id)
            .ToListAsync();
    }

    public Task<List<FinancialTransaction>> GetTransactionsForMonthAsync(DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        return _database.Table<FinancialTransaction>()
            .Where(transaction => transaction.Date >= start && transaction.Date < end)
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

    public Task DeleteRecurringItemAsync(RecurringItem item) =>
        _database.RunInTransactionAsync(connection =>
        {
            var source = $"Recurring:{item.Id}";
            var futureTransactions = connection.Table<FinancialTransaction>()
                .Where(transaction => transaction.Source == source && transaction.Date >= DateTime.Today)
                .ToList();

            foreach (var transaction in futureTransactions)
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
            }

            connection.Delete(item);
        });

    public Task<List<CreditCard>> GetCreditCardsAsync() => _database.Table<CreditCard>().OrderBy(card => card.Name).ToListAsync();

    public async Task<BenefitCard?> GetBenefitCardAsync(BenefitCardKind kind) =>
        await _database.Table<BenefitCard>().Where(card => card.Kind == kind).FirstOrDefaultAsync();

    public Task<List<BenefitCardMovement>> GetBenefitMovementsAsync(int cardId) =>
        _database.Table<BenefitCardMovement>().Where(item => item.CardId == cardId)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();

    public Task SaveBenefitCardAsync(BenefitCard input) => _database.RunInTransactionAsync(connection =>
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.MonthlyAmount <= 0 ||
            input.ExpirationMonth is < 1 or > 12 || input.ExpirationYear < 1 || input.RenewalDay is < 1 or > 31)
            throw new InvalidOperationException("Kart bilgilerini kontrol et.");
        input.Name = input.Name.Trim();

        var existing = connection.Table<BenefitCard>().FirstOrDefault(card => card.Kind == input.Kind);
        if (input.Id == 0)
        {
            if (existing is not null) throw new InvalidOperationException("Bu türde yalnızca bir kart eklenebilir.");
            input.Unit = input.Kind == BenefitCardKind.Meal ? BenefitUnit.Money : input.Unit;
            input.RenewalDay = input.Kind == BenefitCardKind.Meal ? 1 : input.RenewalDay;
            input.CurrentBalance = input.MonthlyAmount;
            input.UsedThisCycle = 0;
            input.LastRenewalDate = DateTime.Today;
            connection.Insert(input);
            connection.Insert(new BenefitCardMovement
            {
                CardId = input.Id, Kind = input.Kind == BenefitCardKind.Meal ? BenefitMovementKind.Credit : BenefitMovementKind.Renewal,
                Unit = input.Unit, Amount = input.MonthlyAmount, BalanceAfter = input.CurrentBalance,
                Description = input.Kind == BenefitCardKind.Meal ? "İlk aylık yükleme" : "İlk limit"
            });
            return;
        }

        if (existing is null || existing.Id != input.Id) throw new InvalidOperationException("Kart bulunamadı.");
        var oldBalance = existing.CurrentBalance;
        var oldUnit = existing.Unit;
        existing.Name = input.Name.Trim();
        existing.ExpirationMonth = input.ExpirationMonth;
        existing.ExpirationYear = input.ExpirationYear;
        existing.MonthlyAmount = input.MonthlyAmount;
        if (existing.Kind == BenefitCardKind.Fuel)
        {
            if (existing.Unit != input.Unit) existing.UsedThisCycle = 0;
            existing.Unit = input.Unit;
            existing.CurrentBalance = Math.Max(0, input.MonthlyAmount - existing.UsedThisCycle);
            if (existing.RenewalDay != input.RenewalDay) existing.LastRenewalDate = DateTime.Today;
            existing.RenewalDay = input.RenewalDay;
        }
        connection.Update(existing);
        if (oldBalance != existing.CurrentBalance || oldUnit != existing.Unit)
            connection.Insert(new BenefitCardMovement
            {
                CardId = existing.Id, Kind = BenefitMovementKind.Adjustment, Unit = existing.Unit,
                Amount = existing.CurrentBalance - oldBalance, BalanceAfter = existing.CurrentBalance,
                Description = "Limit güncellendi"
            });
    });

    public async Task AddBenefitMovementAsync(int cardId, BenefitMovementKind kind, decimal amount, string description)
    {
        await ProcessBenefitRenewalsAsync();
        await _database.RunInTransactionAsync(connection =>
        {
            var card = connection.Find<BenefitCard>(cardId) ?? throw new InvalidOperationException("Kart bulunamadı.");
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new InvalidOperationException("Sıfırdan büyük, geçerli bir miktar girin.");
            if (kind != BenefitMovementKind.Spend && (kind != BenefitMovementKind.Credit || card.Kind != BenefitCardKind.Meal))
                throw new InvalidOperationException("Bu işlem kart için geçerli değil.");
            if (kind == BenefitMovementKind.Spend && amount > card.CurrentBalance)
                throw new InvalidOperationException("Harcama kullanılabilir miktarı aşamaz.");

            card.CurrentBalance += kind == BenefitMovementKind.Credit ? amount : -amount;
            if (kind == BenefitMovementKind.Spend) card.UsedThisCycle += amount;
            connection.Update(card);
            connection.Insert(new BenefitCardMovement
            {
                CardId = card.Id, Kind = kind, Unit = card.Unit, Amount = amount,
                BalanceAfter = card.CurrentBalance,
                Description = string.IsNullOrWhiteSpace(description)
                    ? (kind == BenefitMovementKind.Credit ? "Bakiye eklendi" : "Harcama") : description.Trim()
            });
        });
    }

    public Task ProcessBenefitRenewalsAsync() => _database.RunInTransactionAsync(connection =>
    {
        var today = DateTime.Today;
        foreach (var card in connection.Table<BenefitCard>().ToList())
        {
            var month = new DateTime(card.LastRenewalDate.Year, card.LastRenewalDate.Month, 1);
            while (month <= today)
            {
                var due = new DateTime(month.Year, month.Month,
                    Math.Min(card.RenewalDay, DateTime.DaysInMonth(month.Year, month.Month)));
                if (due > card.LastRenewalDate.Date && due <= today)
                {
                    if (card.Kind == BenefitCardKind.Meal) card.CurrentBalance += card.MonthlyAmount;
                    else card.CurrentBalance = card.MonthlyAmount;
                    card.UsedThisCycle = 0;
                    card.LastRenewalDate = due;
                    connection.Update(card);
                    connection.Insert(new BenefitCardMovement
                    {
                        CardId = card.Id, Kind = card.Kind == BenefitCardKind.Meal ? BenefitMovementKind.Credit : BenefitMovementKind.Renewal,
                        Unit = card.Unit, Amount = card.MonthlyAmount, BalanceAfter = card.CurrentBalance,
                        Description = card.Kind == BenefitCardKind.Meal ? "Aylık yükleme" : "Aylık limit yenilendi", Date = due
                    });
                }
                month = month.AddMonths(1);
            }
        }
    });

    public Task<int> AddCreditCardAsync(CreditCard card) => _database.InsertAsync(card);

    public Task<int> UpdateCreditCardAsync(CreditCard card) => _database.UpdateAsync(card);

    public Task<int> UpdateCreditCardDetailsAsync(CreditCard card) =>
        _database.ExecuteAsync(
            "UPDATE CreditCard SET Name = ?, LastFourDigits = ?, CardholderName = ?, ExpirationMonth = ?, ExpirationYear = ?, SecureDetailsKey = ?, StatementDay = ?, PaymentDay = ?, CreditLimit = ?, IconColor = ?, RevealSensitive = ? WHERE Id = ?",
            card.Name, card.LastFourDigits, card.CardholderName, card.ExpirationMonth, card.ExpirationYear,
            card.SecureDetailsKey, card.StatementDay, card.PaymentDay, card.CreditLimit, card.IconColor, card.RevealSensitive, card.Id);

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
            connection.DeleteAll<BenefitCardMovement>();
            connection.DeleteAll<BenefitCard>();
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
            BenefitCards = await _database.Table<BenefitCard>().ToListAsync(),
            BenefitMovements = await _database.Table<BenefitCardMovement>().ToListAsync(),
            Statements = await GetStatementsAsync(),
            ExportedAt = DateTime.Now
        };

        await File.WriteAllTextAsync(destinationPath, System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
