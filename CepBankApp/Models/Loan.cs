using SQLite;

namespace CepBankApp.Models;

public class Loan
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    [Indexed]
    public int BankAccountId { get; set; }
    public DateTime FirstPaymentDate { get; set; }
    public DateTime LastPaymentDate { get; set; }
    public decimal InstallmentAmount { get; set; }
    public int InstallmentCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class LoanPayment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int LoanId { get; set; }
    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? BankMovementId { get; set; }
}
