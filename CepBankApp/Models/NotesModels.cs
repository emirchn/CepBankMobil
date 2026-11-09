using SQLite;

namespace CepBankApp.Models;

public class NoteReminder
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Indexed] public DateTime EventDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public ReminderCategory Category { get; set; } = ReminderCategory.Etkinlik;
    [Ignore] public DateTime LastDate => EndDate?.Date ?? EventDate.Date;
    public bool Includes(DateTime day) => EventDate.Date <= day.Date && LastDate >= day.Date;
    // Retained for data compatibility; reminder days are now a shared Notlarım setting.
    public int ReminderDaysMask { get; set; } = 1 << 3;
}

public enum ReminderCategory { Etkinlik, OzelGun, Diger }

public enum ShoppingCategory
{
    Diger,
    Elektronik,
    Giyim,
    AyakkabiCanta,
    EvYasam,
    Mutfak,
    KisiselBakim,
    Saglik,
    SporOutdoor,
    KitapKirtasiye,
    HobiOyuncak,
    BebekCocuk,
    EvcilHayvan,
    OtoAksesuar,
    GidaIcecek,
    Gizli
}

public class ShoppingItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public ShoppingCategory Category { get; set; }
    public string Link { get; set; } = string.Empty;
    public string ImageDataUrl { get; set; } = string.Empty;
    public bool HasImage { get; set; }
    [Indexed] public DateTime CreatedAt { get; set; } = DateTime.Now;
    [Indexed] public bool IsPurchased { get; set; }
    public DateTime? PurchasedAt { get; set; }
}

public class GroceryItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public enum PlaceCategory
{
    Restorant,
    Cafe,
    Tatli,
    YuruyusParkuru,
    PiknikAlani,
    Etkinlik,
    Muze,
    Sergi,
    Konser,
    TarihiYer,
    Sahil,
    Doga,
    Alisveris,
    Diger
}

public enum PlaceBudget
{
    Yok,
    Dusuk,
    Orta,
    Yuksek,
    CokYuksek
}

public class PlaceToVisit
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MapLink { get; set; } = string.Empty;
    [Indexed] public PlaceCategory Category { get; set; }
    [Indexed] public PlaceBudget Budget { get; set; } = PlaceBudget.Yok;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
