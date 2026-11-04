using CepBankApp.Models;

namespace CepBankApp.Services;

public static class PlaceOptions
{
    public static readonly PlaceCategory[] Categories = Enum.GetValues<PlaceCategory>();
    public static readonly PlaceBudget[] Budgets = Enum.GetValues<PlaceBudget>();

    public static string CategoryLabel(PlaceCategory category) => category switch
    {
        PlaceCategory.Restorant => "Restorant",
        PlaceCategory.Cafe => "Cafe",
        PlaceCategory.Tatli => "Tatlı",
        PlaceCategory.YuruyusParkuru => "Yürüyüş Parkuru",
        PlaceCategory.PiknikAlani => "Piknik Alanı",
        PlaceCategory.Etkinlik => "Etkinlik",
        PlaceCategory.Muze => "Müze",
        PlaceCategory.Sergi => "Sergi",
        PlaceCategory.Konser => "Konser",
        PlaceCategory.TarihiYer => "Tarihi Yer",
        PlaceCategory.Sahil => "Sahil",
        PlaceCategory.Doga => "Doğa",
        PlaceCategory.Alisveris => "Alışveriş",
        _ => "Diğer"
    };

    public static string BudgetLabel(PlaceBudget budget) => budget switch
    {
        PlaceBudget.Dusuk => "Düşük",
        PlaceBudget.Orta => "Orta",
        PlaceBudget.Yuksek => "Yüksek",
        PlaceBudget.CokYuksek => "Çok Yüksek",
        _ => "Yok"
    };
}
