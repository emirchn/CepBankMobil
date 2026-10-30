using SQLite;

namespace CepBankApp.Models;

public enum RecurringType
{
    Salary,
    Income,
    Loan,
    Subscription,
    OtherExpense
}

public class RecurringItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public TransactionType TransactionType { get; set; }

    public RecurringType RecurringType { get; set; }

    public int DayOfMonth { get; set; } = 1;

    public DateTime StartDate { get; set; } = DateTime.Today;

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string Category { get; set; } = string.Empty;

    public int? CardId { get; set; }

    public int RemainingInstallments { get; set; }

    public int LoanReminderDaysMask { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
