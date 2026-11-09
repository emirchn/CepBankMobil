using SQLite;

namespace CepBankApp.Models;

public class ImportedCardStatementLine
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int CardId { get; set; }
    public DateTime StatementDate { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsCredit { get; set; }
    public string ExternalKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
