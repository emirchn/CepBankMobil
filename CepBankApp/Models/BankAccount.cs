using SQLite;

namespace CepBankApp.Models;

public class BankAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string BankLogoCode { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public bool ShowOnHome { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum BankMovementKind { Deposit, Spend }

public class BankAccountMovement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int AccountId { get; set; }
    public BankMovementKind Kind { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}
