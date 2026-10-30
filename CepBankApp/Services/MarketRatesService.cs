using System.Text.Json;

namespace CepBankApp.Services;

public sealed record MarketRates(decimal Usd, decimal Eur, decimal GramGold, decimal QuarterGold, DateTime? UpdatedAt);

public sealed class MarketRatesService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private MarketRates? _cached;
    private DateTime _nextRefresh;

    public async Task<MarketRates?> GetAsync()
    {
        if (DateTime.Now < _nextRefresh) return _cached;
        await _gate.WaitAsync();
        try
        {
            if (DateTime.Now < _nextRefresh) return _cached;
            using var response = await Client.GetAsync("https://finans.truncgil.com/v4/today.json");
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var data = await JsonDocument.ParseAsync(stream);
            var root = data.RootElement;
            var rates = new MarketRates(
                root.GetProperty("USD").GetProperty("Selling").GetDecimal(),
                root.GetProperty("EUR").GetProperty("Selling").GetDecimal(),
                root.GetProperty("GRA").GetProperty("Selling").GetDecimal(),
                root.GetProperty("CEYREKALTIN").GetProperty("Selling").GetDecimal(),
                DateTime.TryParse(root.GetProperty("Update_Date").GetString(), out var updated) ? updated : null);
            _cached = rates;
            _nextRefresh = DateTime.Now.AddMinutes(15);
            return rates;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _nextRefresh = DateTime.Now.AddMinutes(5);
            return _cached;
        }
        finally { _gate.Release(); }
    }
}
