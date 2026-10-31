using SQLite;
using CepBankApp.Models;
using CepBankApp.Services;

namespace CepBankApp.Data;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _cardPeriodGate = new(1, 1);
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
        var hadStatementDebt = await _database.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('CreditCard') WHERE name = 'StatementDebt'") > 0;
        await _database.CreateTableAsync<FinancialTransaction>();
        await _database.CreateTableAsync<RecurringItem>();
        await _database.CreateTableAsync<CreditCard>();
        await _database.CreateTableAsync<CardInstallment>();
        await _database.CreateTableAsync<CardStatement>();
        await _database.CreateTableAsync<BenefitCard>();
        await _database.CreateTableAsync<BenefitCardMovement>();
        await _database.CreateTableAsync<BankAccount>();
        await _database.CreateTableAsync<BankAccountMovement>();
        await _database.CreateTableAsync<Loan>();
        await _database.CreateTableAsync<LoanPayment>();
        try { await _database.ExecuteAsync("ALTER TABLE BankAccount ADD COLUMN BankLogoCode TEXT NOT NULL DEFAULT ''"); }
        catch (SQLiteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }

        var cardsToMigrate = await _database.Table<CreditCard>().ToListAsync();
        foreach (var card in cardsToMigrate)
        {
            if (card.LastStatementDate == default)
            {
                // Older cards have no period ledger; treat their outstanding total as payable.
                card.StatementDebt = card.CurrentDebt;
                card.CurrentPeriodDebt = 0;
                card.LastStatementDate = CardBilling.LastClosedStatement(DateTime.Today, card.StatementDay);
            }
            else if (!hadStatementDebt)
            {
                // The earlier period model put unpaid statements back into CurrentPeriodDebt.
                var carryKey = $"Carry:{card.Id}:{card.LastStatementDate:yyyyMMdd}";
                var carry = await _database.Table<FinancialTransaction>()
                    .Where(item => item.CardId == card.Id && item.Source == carryKey).FirstOrDefaultAsync();
                if (carry is not null)
                {
                    var payments = (await _database.Table<FinancialTransaction>()
                        .Where(item => item.CardId == card.Id && item.CardDebtChange < 0 && item.Date > card.LastStatementDate)
                        .ToListAsync()).Sum(item => -item.CardDebtChange);
                    card.StatementDebt = Math.Min(card.CurrentDebt, Math.Max(0, carry.Amount - payments));
                    card.CurrentPeriodDebt = Math.Max(0, card.CurrentPeriodDebt - card.StatementDebt);
                }
            }
            await _database.UpdateAsync(card);
        }

        // Old salary rules stay in the backup, but no longer create income transactions.
        await _database.ExecuteAsync("UPDATE RecurringItem SET IsActive = 0 WHERE TransactionType = 0");
        await _database.ExecuteAsync("UPDATE RecurringItem SET IsActive = 0 WHERE Category = 'Alışveriş Taksidi'");

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
        await ProcessCardPeriodsAsync();
    }

    public async Task ProcessCardPeriodsAsync(DateTime? asOf = null)
    {
        await _cardPeriodGate.WaitAsync();
        try
        {
            await _database.RunInTransactionAsync(connection =>
            {
                var today = asOf?.Date ?? DateTime.Today;
                foreach (var card in connection.Table<CreditCard>().Where(item => item.IsActive).ToList())
                {
                    var last = card.LastStatementDate == default
                        ? CardBilling.LastClosedStatement(today, card.StatementDay) : card.LastStatementDate.Date;
                    var latest = CardBilling.LastClosedStatement(today, card.StatementDay);
                    while (last < latest)
                    {
                        var closing = CardBilling.NextStatement(last, card.StatementDay);
                        var nextCutoff = CardBilling.NextStatement(closing, card.StatementDay);
                        card.StatementDebt += card.CurrentPeriodDebt;
                        var carry = card.StatementDebt;
                        if (carry > 0)
                        {
                            connection.Insert(new FinancialTransaction
                            {
                                CardId = card.Id, Type = TransactionType.Expense,
                                Amount = carry, Description = "Önceki aydan kalan borç devri",
                                Category = "Borç Devri", Date = closing,
                                CardStatementDate = nextCutoff, Source = $"Carry:{card.Id}:{closing:yyyyMMdd}",
                                CardDebtChange = 0, CardPeriodChange = 0
                            });
                        }
                        var due = connection.Table<CardInstallment>()
                            .Where(item => item.CardId == card.Id && item.StatementDate == nextCutoff && !item.IsApplied).ToList();
                        foreach (var installment in due)
                        {
                            installment.IsApplied = true;
                            connection.Update(installment);
                            connection.Insert(new FinancialTransaction
                            {
                                CardId = card.Id, Type = TransactionType.Expense,
                                Amount = installment.Amount,
                                Description = $"Taksit {installment.Number}/{installment.TotalCount}",
                                Category = "Taksit", Date = closing,
                                CardStatementDate = nextCutoff, Source = $"Installment:{installment.Id}",
                                CardDebtChange = 0, CardPeriodChange = installment.Amount
                            });
                        }
                        card.CurrentPeriodDebt = due.Sum(item => item.Amount);
                        last = closing;
                    }
                    card.LastStatementDate = last;
                    var futureInstallments = connection.Table<CardInstallment>()
                        .Where(item => item.CardId == card.Id && !item.IsApplied).ToList().Sum(item => item.Amount);
                    var accountedDebt = card.StatementDebt + card.CurrentPeriodDebt + futureInstallments;
                    if (card.CurrentDebt < accountedDebt)
                        card.CurrentDebt = accountedDebt;
                    connection.Update(card);
                }
            });
        }
        finally { _cardPeriodGate.Release(); }
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

    public async Task AddTransactionAsync(FinancialTransaction transaction)
    {
        await ProcessCardPeriodsAsync();
        await _database.RunInTransactionAsync(connection =>
        {
            if (transaction.CardId is int cardId)
            {
                var card = connection.Find<CreditCard>(cardId);
                if (card is null || !card.IsActive)
                    throw new InvalidOperationException("Seçilen kart bulunamadı.");
                if (transaction.Type != TransactionType.Expense)
                    throw new InvalidOperationException("Kart yalnızca gider işlemlerinde seçilebilir.");

                var isPayment = transaction.Category == "Kredi Kartı Ödemesi";
                if (isPayment) throw new InvalidOperationException("Kart ödemesi için banka hesabı seçin.");
                if (transaction.Amount <= 0 || decimal.Round(transaction.Amount, 2) != transaction.Amount ||
                    transaction.InstallmentCount is < 1 or > 36)
                    throw new InvalidOperationException("Geçerli tutar ve 1-36 arası taksit sayısı girin.");
                var openPeriodEnd = CardBilling.NextStatement(card.LastStatementDate.Date, card.StatementDay);
                if (transaction.Source == "Manual" &&
                    (transaction.Date.Date < card.LastStatementDate.Date || transaction.Date.Date >= openPeriodEnd))
                    throw new InvalidOperationException("Kart işlemi yalnızca açık hesap kesim dönemine kaydedilebilir.");

                var installmentAmount = decimal.Floor(transaction.Amount * 100 / transaction.InstallmentCount) / 100;
                if (installmentAmount <= 0) throw new InvalidOperationException("Tutar taksit sayısına göre çok düşük.");
                var firstCutoff = CardBilling.StatementDate(transaction.Source == "Manual" ? transaction.Date : DateTime.Today, card.StatementDay);
                transaction.CardDebtChange = transaction.Amount;
                transaction.CardPeriodChange = installmentAmount;
                transaction.CardStatementDate = firstCutoff;
                card.CurrentDebt += transaction.Amount;
                card.CurrentPeriodDebt += installmentAmount;
                connection.Update(card);
                connection.Insert(transaction);
                var cutoff = firstCutoff;
                for (var number = 2; number <= transaction.InstallmentCount; number++)
                {
                    cutoff = CardBilling.NextStatement(cutoff, card.StatementDay);
                    var amount = number == transaction.InstallmentCount
                        ? transaction.Amount - installmentAmount * (transaction.InstallmentCount - 1)
                        : installmentAmount;
                    connection.Insert(new CardInstallment
                    {
                        CardId = card.Id, PurchaseTransactionId = transaction.Id,
                        StatementDate = cutoff, Amount = amount, Number = number,
                        TotalCount = transaction.InstallmentCount
                    });
                }
                return;
            }
            connection.Insert(transaction);
        });
    }

    public async Task PayCreditCardAsync(int cardId, int accountId, decimal amount, string description)
    {
        await ProcessCardPeriodsAsync();
        await _database.RunInTransactionAsync(connection =>
        {
            var card = connection.Find<CreditCard>(cardId);
            var account = connection.Find<BankAccount>(accountId);
            if (card is null || !card.IsActive) throw new InvalidOperationException("Seçilen kart bulunamadı.");
            if (account is null) throw new InvalidOperationException("Seçilen banka hesabı bulunamadı.");
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new InvalidOperationException("Sıfırdan büyük, geçerli bir TL tutarı girin.");
            if (amount > card.StatementDebt + card.CurrentPeriodDebt)
                throw new InvalidOperationException("Ödeme ödenebilir borcu aşamaz.");
            if (amount > account.CurrentBalance)
                throw new InvalidOperationException("Banka hesabı bakiyesi yetersiz.");

            card.CurrentDebt -= amount;
            var statementPayment = Math.Min(amount, card.StatementDebt);
            card.StatementDebt -= statementPayment;
            card.CurrentPeriodDebt -= amount - statementPayment;
            account.CurrentBalance -= amount;
            connection.Update(card);
            connection.Update(account);
            var transaction = new FinancialTransaction
            {
                Date = DateTime.Now, Type = TransactionType.Expense, CardId = cardId,
                Amount = amount, Description = string.IsNullOrWhiteSpace(description) ? "Kredi kartı ödemesi" : description.Trim(),
                Category = "Kredi Kartı Ödemesi", Source = "CardPayment",
                CardDebtChange = -amount, CardPeriodChange = -(amount - statementPayment)
            };
            connection.Insert(transaction);
            var movement = new BankAccountMovement
            {
                AccountId = accountId, Kind = BankMovementKind.Spend,
                Amount = amount, BalanceAfter = account.CurrentBalance,
                Description = $"{card.Name} kredi kartı ödemesi", Date = transaction.Date
            };
            connection.Insert(movement);
            transaction.BankMovementId = movement.Id;
            connection.Update(transaction);
        });
    }

    public async Task AdjustCreditCardStatementDebtAsync(int cardId, decimal newAmount)
    {
        await ProcessCardPeriodsAsync();
        await _database.RunInTransactionAsync(connection =>
        {
            var card = connection.Find<CreditCard>(cardId);
            if (card is null || !card.IsActive) throw new InvalidOperationException("Kart bulunamadı.");
            if (newAmount < 0 || decimal.Round(newAmount, 2) != newAmount)
                throw new InvalidOperationException("Ekstre borcu sıfır veya daha büyük, iki ondalıklı bir tutar olmalı.");
            var difference = newAmount - card.StatementDebt;
            if (difference == 0) return;
            if (card.CurrentDebt + difference < 0)
                throw new InvalidOperationException("Güncel borç sıfırın altına düşemez.");
            card.StatementDebt = newAmount;
            card.CurrentDebt += difference;
            connection.Update(card);
            connection.Insert(new FinancialTransaction
            {
                CardId = card.Id, Date = DateTime.Now,
                Type = difference > 0 ? TransactionType.Expense : TransactionType.Income,
                Amount = Math.Abs(difference), Description = "Ekstre tutarı güncellemesi",
                Category = "Ekstre Tutarı Güncellemesi", Source = "StatementAdjustment",
                CardDebtChange = difference, CardPeriodChange = 0,
                CardStatementDate = card.LastStatementDate
            });
        });
    }

    public async Task DeleteTransactionAsync(FinancialTransaction transaction)
    {
        await ProcessCardPeriodsAsync();
        await _database.RunInTransactionAsync(connection =>
        {
            if (transaction.Category == "Kredi Kartı Ödemesi" || transaction.Source == "StatementAdjustment" || transaction.BankMovementId.HasValue || transaction.InstallmentCount > 1 || transaction.Source.StartsWith("Carry:") ||
                transaction.Source.StartsWith("Installment:"))
                throw new InvalidOperationException("Bu kart hareketi silinemez.");
            if (transaction.CardId is int cardId && transaction.CardDebtChange != 0)
            {
                var card = connection.Find<CreditCard>(cardId);
                if (card is not null)
                {
                    card.CurrentDebt = Math.Max(0, card.CurrentDebt - transaction.CardDebtChange);
                    card.CurrentPeriodDebt = Math.Max(0, card.CurrentPeriodDebt - transaction.CardPeriodChange);
                    connection.Update(card);
                }
            }
            connection.Delete(transaction);
        });
    }

    public Task<List<RecurringItem>> GetRecurringItemsAsync() => _database.Table<RecurringItem>().OrderBy(item => item.DayOfMonth).ToListAsync();

    public Task<List<BankAccount>> GetBankAccountsAsync() =>
        _database.Table<BankAccount>().OrderBy(account => account.Name).ToListAsync();

    public Task<List<Loan>> GetLoansAsync() => _database.Table<Loan>()
        .OrderByDescending(item => item.CreatedAt).ToListAsync();

    public Task<List<LoanPayment>> GetLoanPaymentsAsync(int loanId) => _database.Table<LoanPayment>()
        .Where(item => item.LoanId == loanId).OrderBy(item => item.DueDate).ToListAsync();

    public Task AddLoanAsync(Loan loan) => _database.RunInTransactionAsync(connection =>
    {
        if (string.IsNullOrWhiteSpace(loan.Name) || loan.InstallmentAmount <= 0 ||
            decimal.Round(loan.InstallmentAmount, 2) != loan.InstallmentAmount ||
            loan.FirstPaymentDate.Date > loan.LastPaymentDate.Date ||
            connection.Find<BankAccount>(loan.BankAccountId) is null)
            throw new InvalidOperationException("Kredi bilgilerini ve banka hesabını kontrol et.");

        var first = loan.FirstPaymentDate.Date;
        var last = loan.LastPaymentDate.Date;
        var monthCount = (last.Year - first.Year) * 12 + last.Month - first.Month + 1;
        if (monthCount is < 1 or > 360)
            throw new InvalidOperationException("Kredi süresi 1 ile 360 ay arasında olmalı.");
        if (monthCount == 1 && first != last)
            throw new InvalidOperationException("Tek taksitli kredide ilk ve son ödeme günü aynı olmalı.");

        loan.Name = loan.Name.Trim();
        loan.FirstPaymentDate = first;
        loan.LastPaymentDate = last;
        loan.InstallmentCount = monthCount;
        connection.Insert(loan);
        for (var index = 0; index < monthCount; index++)
        {
            var month = new DateTime(first.Year, first.Month, 1).AddMonths(index);
            var due = index == 0 ? first : index == monthCount - 1 ? last :
                new DateTime(month.Year, month.Month, Math.Min(first.Day, DateTime.DaysInMonth(month.Year, month.Month)));
            connection.Insert(new LoanPayment
            {
                LoanId = loan.Id, Number = index + 1, DueDate = due,
                Amount = loan.InstallmentAmount
            });
        }
    });

    public Task PayLoanInstallmentAsync(int paymentId) => _database.RunInTransactionAsync(connection =>
    {
        var payment = connection.Find<LoanPayment>(paymentId) ?? throw new InvalidOperationException("Taksit bulunamadı.");
        if (payment.IsPaid) throw new InvalidOperationException("Bu taksit zaten ödendi.");
        if (payment.DueDate.Date > DateTime.Today) throw new InvalidOperationException("Ödeme günü henüz gelmedi.");
        var loan = connection.Find<Loan>(payment.LoanId) ?? throw new InvalidOperationException("Kredi bulunamadı.");
        var account = connection.Find<BankAccount>(loan.BankAccountId) ?? throw new InvalidOperationException("Bağlı banka hesabı bulunamadı.");
        if (account.CurrentBalance < payment.Amount) throw new InvalidOperationException("Banka hesabı bakiyesi yetersiz.");
        account.CurrentBalance -= payment.Amount;
        connection.Update(account);
        var movement = new BankAccountMovement
        {
            AccountId = account.Id, Kind = BankMovementKind.Spend, Amount = payment.Amount,
            BalanceAfter = account.CurrentBalance, Date = DateTime.Now,
            Description = $"{loan.Name} kredi taksiti {payment.Number}/{loan.InstallmentCount}"
        };
        connection.Insert(movement);
        payment.IsPaid = true;
        payment.PaidAt = DateTime.Now;
        payment.BankMovementId = movement.Id;
        connection.Update(payment);
    });

    public async Task<BankAccount?> GetBankAccountAsync(int id) => await _database.FindAsync<BankAccount>(id);

    public async Task<BankAccount?> GetHomeBankAccountAsync() =>
        await _database.Table<BankAccount>().Where(account => account.ShowOnHome).FirstOrDefaultAsync();

    public Task<List<BankAccountMovement>> GetBankMovementsForMonthAsync(int accountId, DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        return _database.Table<BankAccountMovement>()
            .Where(item => item.AccountId == accountId && item.Date >= start && item.Date < end)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();
    }

    public Task SaveBankAccountAsync(BankAccount input) => _database.RunInTransactionAsync(connection =>
    {
        var iban = new string(input.Iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(input.Name) || !IsValidIban(iban))
            throw new InvalidOperationException("Hesap adı ve geçerli bir IBAN girin.");
        if (!BankBrand.Choices.Any(brand => brand.Code == input.BankLogoCode))
            throw new InvalidOperationException("Geçerli bir banka simgesi seçin.");
        var duplicate = connection.Table<BankAccount>().FirstOrDefault(account => account.Iban == iban && account.Id != input.Id);
        if (duplicate is not null) throw new InvalidOperationException("Bu IBAN zaten kayıtlı.");
        if (input.Id == 0)
        {
            input.Name = input.Name.Trim();
            input.Iban = iban;
            input.ShowOnHome = !connection.Table<BankAccount>().Any();
            connection.Insert(input);
        }
        else
        {
            var existing = connection.Find<BankAccount>(input.Id) ?? throw new InvalidOperationException("Hesap bulunamadı.");
            existing.Name = input.Name.Trim();
            existing.Iban = iban;
            existing.BankLogoCode = input.BankLogoCode;
            connection.Update(existing);
        }
    });

    public Task DeleteBankAccountAsync(int accountId) => _database.RunInTransactionAsync(connection =>
    {
        var account = connection.Find<BankAccount>(accountId) ?? throw new InvalidOperationException("Hesap bulunamadı.");
        var movements = connection.Table<BankAccountMovement>().Where(item => item.AccountId == accountId).ToList();
        var movementIds = movements.Select(item => item.Id).ToHashSet();
        foreach (var transaction in connection.Table<FinancialTransaction>().ToList()
                     .Where(item => item.BankMovementId.HasValue && movementIds.Contains(item.BankMovementId.Value)))
        {
            transaction.BankMovementId = null;
            connection.Update(transaction);
        }
        foreach (var loan in connection.Table<Loan>().Where(item => item.BankAccountId == accountId).ToList())
        {
            foreach (var payment in connection.Table<LoanPayment>().Where(item => item.LoanId == loan.Id).ToList())
                connection.Delete(payment);
            connection.Delete(loan);
        }
        foreach (var movement in movements) connection.Delete(movement);
        connection.Delete(account);
        if (account.ShowOnHome)
        {
            var next = connection.Table<BankAccount>().OrderBy(item => item.Name).FirstOrDefault();
            if (next is not null) { next.ShowOnHome = true; connection.Update(next); }
        }
    });

    private static bool IsValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34 ||
            (iban.StartsWith("TR", StringComparison.Ordinal) && iban.Length != 26) ||
            !iban.Take(2).All(character => character is >= 'A' and <= 'Z') ||
            !iban.Skip(2).Take(2).All(character => character is >= '0' and <= '9') ||
            !iban.Skip(4).All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9')) return false;

        var checksum = 0;
        foreach (var character in iban[4..].Concat(iban[..4]))
        {
            if (character is >= '0' and <= '9') checksum = (checksum * 10 + character - '0') % 97;
            else
            {
                var value = character - 'A' + 10;
                checksum = (checksum * 100 + value) % 97;
            }
        }
        return checksum == 1;
    }

    public Task SetHomeBankAccountAsync(int accountId) => _database.RunInTransactionAsync(connection =>
    {
        if (connection.Find<BankAccount>(accountId) is null) throw new InvalidOperationException("Hesap bulunamadı.");
        connection.Execute("UPDATE BankAccount SET ShowOnHome = 0 WHERE ShowOnHome = 1");
        connection.Execute("UPDATE BankAccount SET ShowOnHome = 1 WHERE Id = ?", accountId);
    });

    public Task AddBankMovementAsync(int accountId, BankMovementKind kind, decimal amount) =>
        _database.RunInTransactionAsync(connection =>
        {
            if (amount <= 0 || decimal.Round(amount, 2) != amount)
                throw new InvalidOperationException("Sıfırdan büyük, geçerli bir TL tutarı girin.");
            var account = connection.Find<BankAccount>(accountId) ?? throw new InvalidOperationException("Hesap bulunamadı.");
            if (kind == BankMovementKind.Spend && amount > account.CurrentBalance)
                throw new InvalidOperationException("Harcama güncel bakiyeyi aşamaz.");
            account.CurrentBalance += kind == BankMovementKind.Deposit ? amount : -amount;
            connection.Update(account);
            connection.Insert(new BankAccountMovement
            {
                AccountId = accountId, Kind = kind, Amount = amount, BalanceAfter = account.CurrentBalance,
                Description = kind == BankMovementKind.Deposit ? "Bakiye eklendi" : "Harcama"
            });
        });

    public Task<int> AddRecurringItemAsync(RecurringItem item)
    {
        if (item.RecurringType != RecurringType.Subscription || item.TransactionType != TransactionType.Expense ||
            !item.CardId.HasValue || string.IsNullOrWhiteSpace(item.Name) || item.Amount <= 0)
            throw new InvalidOperationException("Abonelik için geçerli bir kart ve tutar girin.");
        return _database.InsertAsync(item);
    }

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
                        card.CurrentPeriodDebt = Math.Max(0, card.CurrentPeriodDebt - transaction.CardPeriodChange);
                        connection.Update(card);
                    }
                }
                connection.Delete(transaction);
            }

            connection.Delete(item);
        });

    public async Task<List<CreditCard>> GetCreditCardsAsync()
    {
        await ProcessCardPeriodsAsync();
        return await _database.Table<CreditCard>().OrderBy(card => card.Name).ToListAsync();
    }

    public Task<List<FinancialTransaction>> GetCardTransactionsForCurrentPeriodAsync(CreditCard card)
    {
        var cardId = card.Id;
        var start = card.LastStatementDate.Date;
        var end = DateTime.Today.AddDays(1);
        return _database.Table<FinancialTransaction>()
            .Where(item => item.CardId == cardId && item.Date >= start && item.Date < end)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();
    }

    public Task<List<FinancialTransaction>> GetRecentCardTransactionsAsync(int cardId, int count = 10) =>
        _database.Table<FinancialTransaction>()
            .Where(item => item.CardId == cardId)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id)
            .Take(count).ToListAsync();

    public Task<List<FinancialTransaction>> GetCardInstallmentPurchasesAsync(int cardId) =>
        _database.Table<FinancialTransaction>()
            .Where(item => item.CardId == cardId && item.InstallmentCount > 1 && item.CardDebtChange > 0)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();

    public Task<List<CardInstallment>> GetCardInstallmentsAsync(int cardId) =>
        _database.Table<CardInstallment>()
            .Where(item => item.CardId == cardId)
            .OrderBy(item => item.StatementDate).ThenBy(item => item.Number).ToListAsync();

    public Task ResetCreditCardAsync(int cardId) => _database.RunInTransactionAsync(connection =>
    {
        var card = connection.Find<CreditCard>(cardId) ?? throw new InvalidOperationException("Kart bulunamadı.");
        var transactions = connection.Table<FinancialTransaction>().Where(item => item.CardId == cardId).ToList();
        var paymentMovementIds = transactions.Where(item => item.BankMovementId.HasValue)
            .Select(item => item.BankMovementId!.Value).ToHashSet();
        var paymentMovements = connection.Table<BankAccountMovement>().ToList()
            .Where(item => paymentMovementIds.Contains(item.Id)).ToList();
        foreach (var group in paymentMovements.GroupBy(item => item.AccountId))
        {
            var accountId = group.Key;
            var account = connection.Find<BankAccount>(accountId);
            if (account is null) continue;
            account.CurrentBalance += group.Sum(item => item.Amount);
            connection.Update(account);
            var removed = group.OrderBy(item => item.Id).ToList();
            foreach (var movement in connection.Table<BankAccountMovement>()
                         .Where(item => item.AccountId == accountId).ToList())
            {
                if (paymentMovementIds.Contains(movement.Id)) continue;
                var refund = removed.Where(item => item.Id < movement.Id).Sum(item => item.Amount);
                if (refund == 0) continue;
                movement.BalanceAfter += refund;
                connection.Update(movement);
            }
        }
        foreach (var movement in paymentMovements) connection.Delete(movement);
        foreach (var transaction in transactions) connection.Delete(transaction);
        foreach (var installment in connection.Table<CardInstallment>().Where(item => item.CardId == cardId).ToList())
            connection.Delete(installment);
        foreach (var statement in connection.Table<CardStatement>().Where(item => item.CardId == cardId).ToList())
            connection.Delete(statement);
        foreach (var recurring in connection.Table<RecurringItem>().Where(item => item.CardId == cardId).ToList())
            connection.Delete(recurring);
        card.CurrentDebt = 0;
        card.StatementDebt = 0;
        card.CurrentPeriodDebt = 0;
        card.LastStatementDate = CardBilling.LastClosedStatement(DateTime.Today, card.StatementDay);
        connection.Update(card);
    });

    public async Task<BenefitCard?> GetBenefitCardAsync(BenefitCardKind kind) =>
        await _database.Table<BenefitCard>().Where(card => card.Kind == kind).FirstOrDefaultAsync();

    public Task<List<BenefitCardMovement>> GetBenefitMovementsAsync(int cardId) =>
        _database.Table<BenefitCardMovement>().Where(item => item.CardId == cardId)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).ToListAsync();

    public Task SaveBenefitCardAsync(BenefitCard input) => _database.RunInTransactionAsync(connection =>
    {
        if (string.IsNullOrWhiteSpace(input.Name) ||
            (input.Kind == BenefitCardKind.Fuel && input.MonthlyAmount <= 0) ||
            input.ExpirationMonth is < 1 or > 12 || input.ExpirationYear < 1 || input.RenewalDay is < 1 or > 31)
            throw new InvalidOperationException("Kart bilgilerini kontrol et.");
        input.Name = input.Name.Trim();

        var existing = connection.Table<BenefitCard>().FirstOrDefault(card => card.Kind == input.Kind);
        if (input.Id == 0)
        {
            if (existing is not null) throw new InvalidOperationException("Bu türde yalnızca bir kart eklenebilir.");
            input.Unit = input.Kind == BenefitCardKind.Meal ? BenefitUnit.Money : input.Unit;
            input.RenewalDay = input.Kind == BenefitCardKind.Meal ? 1 : input.RenewalDay;
            input.CurrentBalance = input.Kind == BenefitCardKind.Meal ? 0 : input.MonthlyAmount;
            input.UsedThisCycle = 0;
            input.LastRenewalDate = DateTime.Today;
            connection.Insert(input);
            if (input.Kind == BenefitCardKind.Fuel)
                connection.Insert(new BenefitCardMovement
                {
                    CardId = input.Id, Kind = BenefitMovementKind.Renewal,
                    Unit = input.Unit, Amount = input.MonthlyAmount, BalanceAfter = input.CurrentBalance,
                    Description = "İlk limit"
                });
            return;
        }

        if (existing is null || existing.Id != input.Id) throw new InvalidOperationException("Kart bulunamadı.");
        var oldBalance = existing.CurrentBalance;
        var oldUnit = existing.Unit;
        existing.Name = input.Name.Trim();
        existing.ExpirationMonth = input.ExpirationMonth;
        existing.ExpirationYear = input.ExpirationYear;
        existing.MonthlyAmount = existing.Kind == BenefitCardKind.Meal ? 0 : input.MonthlyAmount;
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
            if (card.Kind == BenefitCardKind.Meal) continue;
            var month = new DateTime(card.LastRenewalDate.Year, card.LastRenewalDate.Month, 1);
            while (month <= today)
            {
                var due = new DateTime(month.Year, month.Month,
                    Math.Min(card.RenewalDay, DateTime.DaysInMonth(month.Year, month.Month)));
                if (due > card.LastRenewalDate.Date && due <= today)
                {
                    card.CurrentBalance = card.MonthlyAmount;
                    card.UsedThisCycle = 0;
                    card.LastRenewalDate = due;
                    connection.Update(card);
                    connection.Insert(new BenefitCardMovement
                    {
                        CardId = card.Id, Kind = BenefitMovementKind.Renewal,
                        Unit = card.Unit, Amount = card.MonthlyAmount, BalanceAfter = card.CurrentBalance,
                        Description = "Aylık limit yenilendi", Date = due
                    });
                }
                month = month.AddMonths(1);
            }
        }
    });

    public Task<int> AddCreditCardAsync(CreditCard card)
    {
        card.LastStatementDate = CardBilling.LastClosedStatement(DateTime.Today, card.StatementDay);
        return _database.InsertAsync(card);
    }

    public Task UpdateCreditCardDetailsAsync(CreditCard card) => _database.RunInTransactionAsync(connection =>
    {
        var existing = connection.Find<CreditCard>(card.Id) ?? throw new InvalidOperationException("Kart bulunamadı.");
        var statementDayChanged = existing.StatementDay != card.StatementDay;
        if (statementDayChanged && existing.CurrentDebt > 0)
            throw new InvalidOperationException("Borç varken hesap kesim günü değiştirilemez.");
        existing.Name = card.Name;
        existing.LastFourDigits = card.LastFourDigits;
        existing.CardholderName = card.CardholderName;
        existing.ExpirationMonth = card.ExpirationMonth;
        existing.ExpirationYear = card.ExpirationYear;
        existing.SecureDetailsKey = card.SecureDetailsKey;
        existing.StatementDay = card.StatementDay;
        existing.PaymentDay = card.PaymentDay;
        existing.CreditLimit = card.CreditLimit;
        existing.IconColor = card.IconColor;
        existing.RevealSensitive = card.RevealSensitive;
        if (statementDayChanged)
            existing.LastStatementDate = CardBilling.LastClosedStatement(DateTime.Today, card.StatementDay);
        connection.Update(existing);
    });

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

    public Task ClearAllDataAsync() =>
        _database.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<LoanPayment>();
            connection.DeleteAll<Loan>();
            connection.DeleteAll<CardStatement>();
            connection.DeleteAll<CardInstallment>();
            connection.DeleteAll<BenefitCardMovement>();
            connection.DeleteAll<BenefitCard>();
            connection.DeleteAll<BankAccountMovement>();
            connection.DeleteAll<BankAccount>();
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
            CardInstallments = await _database.Table<CardInstallment>().ToListAsync(),
            BenefitCards = await _database.Table<BenefitCard>().ToListAsync(),
            BenefitMovements = await _database.Table<BenefitCardMovement>().ToListAsync(),
            BankAccounts = await GetBankAccountsAsync(),
            BankMovements = await _database.Table<BankAccountMovement>().ToListAsync(),
            Loans = await GetLoansAsync(),
            LoanPayments = await _database.Table<LoanPayment>().ToListAsync(),
            Statements = await GetStatementsAsync(),
            ExportedAt = DateTime.Now
        };

        await File.WriteAllTextAsync(destinationPath, System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}
