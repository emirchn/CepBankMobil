using SQLite;

namespace CepBankApp.Models;

public class CreditCard
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string LastFourDigits { get; set; } = string.Empty;

    public string CardholderName { get; set; } = string.Empty;

    public int ExpirationMonth { get; set; }

    public int ExpirationYear { get; set; }

    // Full number and verification code are stored under this key in SecureStorage.
    public string SecureDetailsKey { get; set; } = string.Empty;

    public int StatementDay { get; set; } = 1;

    public int PaymentDay { get; set; } = 1;

    public decimal? CreditLimit { get; set; }

    public string IconColor { get; set; } = "#465267";

    public bool RevealSensitive { get; set; }

    public decimal CurrentDebt { get; set; }

    public decimal CurrentPeriodDebt { get; set; }

    public decimal StatementDebt { get; set; }

    [Ignore]
    public decimal FutureInstallmentDebt => Math.Max(0, CurrentDebt - StatementDebt - CurrentPeriodDebt);

    [Ignore]
    public decimal? AvailableLimit => CreditLimit.HasValue
        ? Math.Max(0, CreditLimit.Value - CurrentDebt)
        : null;

    public DateTime LastStatementDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class CardInstallment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int CardId { get; set; }
    public int PurchaseTransactionId { get; set; }
    public DateTime StatementDate { get; set; }
    public decimal Amount { get; set; }
    public int Number { get; set; }
    public int TotalCount { get; set; }
    public bool IsApplied { get; set; }
}

public class CardStatement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int CardId { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime StatementDate { get; set; }

    public DateTime PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }
}
