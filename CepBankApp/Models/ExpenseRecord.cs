using SQLite;

namespace CepBankApp.Models;

public class ExpenseRecord
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    // A snapshot of the label only. No card or account balance is changed.
    public string CardName { get; set; } = string.Empty;
    public string SeriesId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
