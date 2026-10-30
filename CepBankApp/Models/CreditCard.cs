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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
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
