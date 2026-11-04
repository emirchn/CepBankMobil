using CepBankApp.Models;

namespace CepBankApp.Services;

public static class ShoppingCategories
{
    // Broad groups adapted for a personal list from Google Product Taxonomy:
    // https://www.google.com/basepages/producttype/taxonomy-with-ids.en-US.txt
    public static readonly ShoppingCategory[] All = Enum.GetValues<ShoppingCategory>();

    public static string Label(ShoppingCategory category) => category switch
    {
        ShoppingCategory.Elektronik => "Elektronik",
        ShoppingCategory.Giyim => "Giyim ve aksesuar",
        ShoppingCategory.AyakkabiCanta => "Ayakkabı ve çanta",
        ShoppingCategory.EvYasam => "Ev ve yaşam",
        ShoppingCategory.Mutfak => "Mutfak",
        ShoppingCategory.KisiselBakim => "Kişisel bakım ve kozmetik",
        ShoppingCategory.Saglik => "Sağlık",
        ShoppingCategory.SporOutdoor => "Spor ve outdoor",
        ShoppingCategory.KitapKirtasiye => "Kitap ve kırtasiye",
        ShoppingCategory.HobiOyuncak => "Hobi ve oyuncak",
        ShoppingCategory.BebekCocuk => "Bebek ve çocuk",
        ShoppingCategory.EvcilHayvan => "Evcil hayvan",
        ShoppingCategory.OtoAksesuar => "Otomobil aksesuarı",
        ShoppingCategory.GidaIcecek => "Gıda ve içecek",
        ShoppingCategory.Gizli => "Gizli",
        _ => "Diğer"
    };
}
