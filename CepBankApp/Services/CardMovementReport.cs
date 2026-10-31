using CepBankApp.Models;

namespace CepBankApp.Services;

public sealed record CardMovement(int CardId, int TransactionId, DateTime Date, DateTime PurchaseDate,
    DateTime StatementDate, string Description, string Category, decimal Amount,
    int InstallmentNumber, int InstallmentCount, bool IsPayment,
    bool IsAdjustment = false, bool IsDecrease = false);

public static class CardMovementReport
{
    public static List<CardMovement> Build(IEnumerable<FinancialTransaction> transactions,
        IEnumerable<CardInstallment> installments)
    {
        var all = transactions.Where(item => item.CardId.HasValue).ToList();
        var purchases = all.Where(item => item.CardDebtChange > 0 && item.Source != "StatementAdjustment").ToDictionary(item => item.Id);
        var installmentById = installments.ToDictionary(item => item.Id);
        var result = new List<CardMovement>();
        foreach (var item in all)
        {
            if (item.Source.StartsWith("Carry:", StringComparison.Ordinal)) continue;
            if (item.Source == "StatementAdjustment")
            {
                result.Add(new(item.CardId!.Value, item.Id, item.Date, item.Date,
                    item.CardStatementDate ?? item.Date, item.Description, item.Category,
                    item.Amount, 1, 1, false, true, item.CardDebtChange < 0));
                continue;
            }
            if (item.Source.StartsWith("Installment:", StringComparison.Ordinal))
            {
                if (!int.TryParse(item.Source[12..], out var id) || !installmentById.TryGetValue(id, out var part)) continue;
                purchases.TryGetValue(part.PurchaseTransactionId, out var purchase);
                result.Add(new(item.CardId!.Value, item.Id, item.Date, purchase?.Date ?? item.Date,
                    part.StatementDate, purchase?.Description ?? item.Description,
                    purchase?.Category ?? item.Category, part.Amount, part.Number, part.TotalCount, false));
                continue;
            }
            var payment = item.CardDebtChange < 0 || item.Category == "Kredi Kartı Ödemesi";
            if (!payment && item.CardDebtChange <= 0) continue;
            result.Add(new(item.CardId!.Value, item.Id, item.Date, item.Date,
                item.CardStatementDate ?? item.Date, item.Description, item.Category,
                payment ? item.Amount : item.CardPeriodChange,
                1, item.InstallmentCount, payment));
        }
        return result.OrderByDescending(item => item.Date).ThenByDescending(item => item.TransactionId).ToList();
    }

    public static bool InPeriod(CardMovement item, DateTime start, DateTime end) => item.IsPayment || item.IsAdjustment
        ? item.Date.Date >= start.Date && item.Date.Date < end.Date
        : item.StatementDate.Date > start.Date && item.StatementDate.Date <= end.Date;
}
