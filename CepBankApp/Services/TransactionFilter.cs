using CepBankApp.Models;

namespace CepBankApp.Services;

public sealed record TransactionFilter(
    TransactionType? Type = null, decimal? Minimum = null, decimal? Maximum = null,
    string Category = "", int? CardId = null)
{
    public IEnumerable<FinancialTransaction> Apply(IEnumerable<FinancialTransaction> transactions) =>
        transactions.Where(transaction =>
            (!Type.HasValue || transaction.Type == Type) &&
            (!Minimum.HasValue || transaction.Amount >= Minimum) &&
            (!Maximum.HasValue || transaction.Amount <= Maximum) &&
            (string.IsNullOrEmpty(Category) || transaction.Category == Category) &&
            (!CardId.HasValue || transaction.CardId == CardId));

    public int Count => (Type.HasValue ? 1 : 0) + (Minimum.HasValue || Maximum.HasValue ? 1 : 0) +
        (string.IsNullOrEmpty(Category) ? 0 : 1) + (CardId.HasValue ? 1 : 0);
}
