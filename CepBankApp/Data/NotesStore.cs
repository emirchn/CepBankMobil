using CepBankApp.Models;
using SQLite;

namespace CepBankApp.Data;

public sealed class NotesStore
{
    private readonly SQLiteAsyncConnection _db = new(Path.Combine(FileSystem.AppDataDirectory, "notes.db3"));
    private readonly object _gate = new();
    private Task? _initialization;

    public Task InitializeAsync()
    {
        lock (_gate) return _initialization ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        await _db.CreateTableAsync<NoteReminder>();
        await _db.CreateTableAsync<ShoppingItem>();
        await _db.CreateTableAsync<GroceryItem>();
        await _db.CreateTableAsync<PlaceToVisit>();
    }

    public async Task<List<NoteReminder>> GetRemindersAsync()
    {
        await InitializeAsync();
        return await _db.Table<NoteReminder>().OrderBy(x => x.EventDate).ToListAsync();
    }

    public async Task<NoteReminder?> GetReminderAsync(int id)
    {
        await InitializeAsync();
        return await _db.Table<NoteReminder>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task SaveReminderAsync(NoteReminder item)
    {
        await InitializeAsync();
        if (item.Id == 0) await _db.InsertAsync(item); else await _db.UpdateAsync(item);
    }

    public async Task DeleteReminderAsync(NoteReminder item)
    {
        await InitializeAsync();
        await _db.DeleteAsync(item);
    }

    public async Task<List<ShoppingItem>> GetShoppingItemsAsync()
    {
        await InitializeAsync();
        // Images are base64 strings and can be several MB each. Fetch the list first,
        // then let the page load images after product information is visible.
        return await _db.QueryAsync<ShoppingItem>(
            "SELECT Id, Name, Price, Category, Link, CreatedAt, IsPurchased, PurchasedAt " +
            "FROM ShoppingItem ORDER BY CreatedAt DESC");
    }

    public async Task<ShoppingItem?> GetShoppingItemAsync(int id)
    {
        await InitializeAsync();
        return await _db.Table<ShoppingItem>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task SaveShoppingItemAsync(ShoppingItem item)
    {
        await InitializeAsync();
        if (item.Id == 0) await _db.InsertAsync(item); else await _db.UpdateAsync(item);
    }

    public async Task SetShoppingPurchasedAsync(int id, bool purchased, DateTime? purchasedAt)
    {
        await InitializeAsync();
        await _db.ExecuteAsync("UPDATE ShoppingItem SET IsPurchased = ?, PurchasedAt = ? WHERE Id = ?",
            purchased, purchasedAt, id);
    }

    public async Task DeleteShoppingItemAsync(ShoppingItem item)
    {
        await InitializeAsync();
        await _db.DeleteAsync(item);
    }

    public async Task<List<GroceryItem>> GetGroceriesAsync()
    {
        await InitializeAsync();
        return await _db.Table<GroceryItem>().OrderBy(x => x.CreatedAt).ToListAsync();
    }

    public async Task SaveGroceryAsync(GroceryItem item)
    {
        await InitializeAsync();
        if (item.Id == 0) await _db.InsertAsync(item); else await _db.UpdateAsync(item);
    }

    public async Task DeleteGroceryAsync(GroceryItem item)
    {
        await InitializeAsync();
        await _db.DeleteAsync(item);
    }

    public async Task ClearGroceriesAsync()
    {
        await InitializeAsync();
        await _db.DeleteAllAsync<GroceryItem>();
    }

    public async Task<List<PlaceToVisit>> GetPlacesAsync()
    {
        await InitializeAsync();
        return await _db.Table<PlaceToVisit>().OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<PlaceToVisit?> GetPlaceAsync(int id)
    {
        await InitializeAsync();
        return await _db.Table<PlaceToVisit>().Where(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task SavePlaceAsync(PlaceToVisit item)
    {
        await InitializeAsync();
        if (item.Id == 0) await _db.InsertAsync(item); else await _db.UpdateAsync(item);
    }

    public async Task DeletePlaceAsync(PlaceToVisit item)
    {
        await InitializeAsync();
        await _db.DeleteAsync(item);
    }

    public async Task ClearAllAsync()
    {
        await InitializeAsync();
        await _db.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<NoteReminder>();
            connection.DeleteAll<ShoppingItem>();
            connection.DeleteAll<GroceryItem>();
            connection.DeleteAll<PlaceToVisit>();
        });
    }
}
