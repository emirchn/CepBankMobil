using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CepBankApp.Services;

public sealed record BankFileRow(DateTime Date, string Description, decimal SignedAmount,
    decimal? BalanceAfter, string Reference);

public sealed record BankFilePreview(string? Iban, decimal? Balance, IReadOnlyList<BankFileRow> Rows);

public sealed record CardFileRow(DateTime Date, string Description, decimal Amount,
    bool IsCredit, int Sequence);

public sealed record CardFilePreview(DateTime StatementDate, string StatementReference,
    string? CardLastFour, IReadOnlyList<CardFileRow> Rows);

public static class StatementFileParser
{
    private static readonly CultureInfo Turkish = new("tr-TR");
    private static readonly Regex PdfTransaction = new(
        @"^\s*(?<date>\d{1,2}\s+\p{L}+\s+\d{4})\s+(?<description>.+?)\s+(?<amount>\d[\d.]*,\d{2})(?<sign>[+-]?)\s*$",
        RegexOptions.Compiled);

    public static BankFilePreview ReadBankFile(Stream stream, string fileName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var isCsv = Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
        var config = new ExcelReaderConfiguration
        {
            FallbackEncoding = Encoding.GetEncoding(1254),
            AutodetectSeparators = [',', ';', '\t']
        };
        using var reader = isCsv ? ExcelReaderFactory.CreateCsvReader(stream, config)
            : ExcelReaderFactory.CreateReader(stream, config);
        string? iban = null;
        decimal? balance = null;
        Dictionary<string, int>? columns = null;
        var rows = new List<BankFileRow>();
        do
        {
            while (reader.Read())
            {
                var cells = Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue).ToArray();
                if (columns is null)
                {
                    var label = CellText(cells.ElementAtOrDefault(0));
                    if (label.Equals("IBAN", StringComparison.OrdinalIgnoreCase))
                        iban = NormalizeIban(CellText(cells.ElementAtOrDefault(1)));
                    if (label.Equals("Bakiye", StringComparison.OrdinalIgnoreCase))
                        balance = ParseAmount(cells.ElementAtOrDefault(1));
                    var found = cells.Select((value, index) => (Name: CellText(value), Index: index))
                        .Where(x => x.Name.Length > 0).ToDictionary(x => x.Name, x => x.Index,
                            StringComparer.OrdinalIgnoreCase);
                    if (found.ContainsKey("Tarih") && found.ContainsKey("Açıklama") &&
                        found.ContainsKey("Tutar")) columns = found;
                    continue;
                }

                object? At(string name) => columns.TryGetValue(name, out var index) && index < cells.Length
                    ? cells[index] : null;
                var dateValue = At("Tarih");
                var amount = ParseAmount(At("Tutar"));
                if (dateValue is null && amount is null) continue;
                var date = ParseDate(dateValue);
                if (date is null || amount is null || amount == 0)
                    throw new InvalidDataException($"Hesap dosyasında {reader.Depth + 1}. satır okunamadı.");
                var description = CellText(At("Açıklama"));
                if (string.IsNullOrWhiteSpace(description)) description = CellText(At("Etiket"));
                if (string.IsNullOrWhiteSpace(description)) description = "Banka işlemi";
                var rowBalance = ParseAmount(At("Bakiye"));
                var reference = CellText(At("Dekont No"));
                if (reference.Length == 0)
                    reference = $"{date:yyyyMMdd}:{amount.Value:0.00}:{description}:{rowBalance:0.00}";
                rows.Add(new(date.Value, description, amount.Value, rowBalance, reference));
            }
            if (columns is not null) break;
        } while (reader.NextResult());
        if (columns is null || rows.Count == 0)
            throw new InvalidDataException("Tarih, Açıklama ve Tutar sütunları olan hesap hareketleri bulunamadı.");
        return new(iban, balance, rows);
    }

    public static CardFilePreview ReadGarantiCardPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var pages = document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToList();
        var allText = string.Join("\n", pages);
        var statementMatch = Regex.Match(allText,
            @"Hesap Kesim Tarihi\s+(?<date>\d{1,2}\s+\p{L}+\s+\d{4})",
            RegexOptions.IgnoreCase);
        var statementDate = statementMatch.Success ? ParseDate(statementMatch.Groups["date"].Value) : null;
        if (statementDate is null)
            throw new InvalidDataException("Garanti kart ekstresinin hesap kesim tarihi okunamadı.");
        var statementNumber = Regex.Match(allText, @"Ekstre No\s*:\s*(?<number>[^\r\n]+)",
            RegexOptions.IgnoreCase).Groups["number"].Value.Trim();
        if (statementNumber.Length == 0)
            throw new InvalidDataException("Garanti kart ekstresinin numarası okunamadı.");
        var cardMatch = Regex.Match(allText, @"Kart Numarası\s+\d{4}\s+\d{2}\*{2}\s+\*{4}\s+(?<last>\d{4})",
            RegexOptions.IgnoreCase);
        var lastFour = cardMatch.Success ? cardMatch.Groups["last"].Value : null;

        var rows = new List<CardFileRow>();
        foreach (var page in pages)
        {
            var header = page.IndexOf("İşlem Tarihi Dönem İçi İşlemler", StringComparison.OrdinalIgnoreCase);
            if (header < 0) continue;
            var body = page[(header + "İşlem Tarihi Dönem İçi İşlemler".Length)..];
            var summary = body.IndexOf("EKSTRE ÖZETİ", StringComparison.OrdinalIgnoreCase);
            if (summary >= 0) body = body[..summary];
            foreach (var line in body.Split('\n'))
            {
                var match = PdfTransaction.Match(line);
                if (!match.Success) continue;
                var date = ParseDate(match.Groups["date"].Value);
                var amount = ParseAmount(match.Groups["amount"].Value);
                if (date is null || amount is null || amount <= 0) continue;
                var description = Regex.Replace(match.Groups["description"].Value.Trim(), @"\s+", " ");
                rows.Add(new(date.Value, description, amount.Value,
                    match.Groups["sign"].Value == "+", rows.Count + 1));
            }
        }
        if (rows.Count == 0)
            throw new InvalidDataException("Garanti kart ekstresinde işlem satırı bulunamadı.");
        return new(statementDate.Value, statementNumber, lastFour, rows);
    }

    private static string CellText(object? value) => value switch
    {
        null => string.Empty,
        string text => text.Trim(),
        DateTime date => date.ToString("dd.MM.yyyy", Turkish),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
    };

    private static string NormalizeIban(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static decimal? ParseAmount(object? value)
    {
        if (value is null) return null;
        if (value is double number) return decimal.Round(Convert.ToDecimal(number), 2);
        if (value is decimal money) return money;
        var text = CellText(value).Replace("TL", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (text.Length == 0) return null;
        var culture = text.Contains(',') ? Turkish : CultureInfo.InvariantCulture;
        return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign,
            culture, out var result) ? result : null;
    }

    private static DateTime? ParseDate(object? value)
    {
        if (value is DateTime date) return date;
        if (value is double serial && serial > 20000 && serial < 100000)
            return DateTime.FromOADate(serial);
        var text = CellText(value);
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy",
            "yyyy-MM-dd", "d MMMM yyyy", "dd MMMM yyyy" };
        return DateTime.TryParseExact(text, formats, Turkish, DateTimeStyles.None, out var parsed)
            ? parsed : null;
    }
}
