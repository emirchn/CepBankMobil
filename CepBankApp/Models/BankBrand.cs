namespace CepBankApp.Models;

public sealed record BankBrand(string Code, string Name, string Mark, string Color)
{
    public static readonly BankBrand Generic = new("", "Diğer banka", "₺", "#465267");

    public static readonly IReadOnlyList<BankBrand> Choices =
    [
        Generic,
        new("ziraat", "Ziraat Bankası", "Z", "#D71920"),
        new("isbank", "İş Bankası", "İŞ", "#075AA7"),
        new("garanti", "Garanti BBVA", "G", "#168B55"),
        new("akbank", "Akbank", "A", "#E51D35"),
        new("yapikredi", "Yapı Kredi", "YK", "#006FB9"),
        new("vakifbank", "VakıfBank", "V", "#E9A900"),
        new("halkbank", "Halkbank", "H", "#1567A3"),
        new("qnb", "QNB", "Q", "#6E3175"),
        new("denizbank", "DenizBank", "D", "#005DA9"),
        new("teb", "TEB", "TEB", "#198549"),
        new("ing", "ING", "ING", "#F27618"),
        new("kuveytturk", "Kuveyt Türk", "KT", "#007F65"),
        new("enpara", "Enpara", "e", "#A5459D")
    ];

    public static BankBrand Find(string? code) => Choices.FirstOrDefault(item => item.Code == code) ?? Generic;
}
