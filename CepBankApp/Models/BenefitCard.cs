using SQLite;

namespace CepBankApp.Models;

public enum BenefitCardKind { Fuel, Meal }
public enum BenefitUnit { Money, Liter }
public enum BenefitMovementKind { Credit, Spend, Renewal, Adjustment }

public class BenefitCard
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed("IX_BenefitCard_Kind", 1, Unique = true)]
    public BenefitCardKind Kind { get; set; }

    public string Name { get; set; } = string.Empty;
    public int ExpirationMonth { get; set; }
    public int ExpirationYear { get; set; }
    public BenefitUnit Unit { get; set; } = BenefitUnit.Money;
    public decimal MonthlyAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal UsedThisCycle { get; set; }
    public int RenewalDay { get; set; } = 1;
    public DateTime LastRenewalDate { get; set; } = DateTime.Today;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class BenefitCardMovement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    [Indexed]
    public int CardId { get; set; }
    public BenefitMovementKind Kind { get; set; }
    public BenefitUnit Unit { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Now;
}
