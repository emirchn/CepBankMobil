using SQLite;

namespace CepBankApp.Models;

public enum TransactionType
{
    Income,
    Expense
}

public class FinancialTransaction
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public int? CardId { get; set; }

    // Positive for a card purchase, negative for a card payment.
    public decimal CardDebtChange { get; set; }

    public DateTime? CardStatementDate { get; set; }

    public string Source { get; set; } = "Manual";

    public bool IsRecurring { get; set; }

    [Ignore]
    public bool IsProjected { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
