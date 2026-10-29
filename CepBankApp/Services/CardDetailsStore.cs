using System.Text.Json;
using Microsoft.Maui.Storage;

namespace CepBankApp.Services;

public sealed class CardPrivateDetails
{
    public string Number { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
}

public static class CardDetailsStore
{
    private static readonly object InitializationLock = new();

    private static void EnsureInitialized()
    {
        lock (InitializationLock)
        {
            if (Preferences.Default.ContainsKey("secure_storage_initialized")) return;
            SecureStorage.Default.RemoveAll();
            Preferences.Default.Set("secure_storage_initialized", true);
        }
    }

    public static async Task<CardPrivateDetails?> GetAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        EnsureInitialized();
        var json = await SecureStorage.Default.GetAsync(key);
        return json is null ? null : JsonSerializer.Deserialize<CardPrivateDetails>(json);
    }

    public static Task SetAsync(string key, CardPrivateDetails details)
    {
        EnsureInitialized();
        var json = JsonSerializer.Serialize(details);
#if IOS || MACCATALYST
        return SecureStorage.Default.SetAsync(key, json, Security.SecAccessible.WhenPasscodeSetThisDeviceOnly);
#else
        return SecureStorage.Default.SetAsync(key, json);
#endif
    }

    public static void Remove(string key)
    {
        if (!string.IsNullOrWhiteSpace(key)) SecureStorage.Default.Remove(key);
    }

    public static bool IsValidNumber(string number)
    {
        if (number.Length is < 12 or > 19 || number.Any(character => character is < '0' or > '9')) return false;
        var sum = 0;
        var doubleDigit = false;
        for (var index = number.Length - 1; index >= 0; index--)
        {
            var digit = number[index] - '0';
            if (doubleDigit) { digit *= 2; if (digit > 9) digit -= 9; }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }
}
