using System.Text.Json;
using CepBankApp.Data;

namespace CepBankApp.Services;

public sealed class WidgetSummaryService(NotesStore notes)
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task TryRefreshAsync()
    {
        try { await RefreshAsync(); }
        catch { /* Financial changes remain saved when the widget is unavailable. */ }
    }

    public async Task RefreshAsync()
    {
#if IOS
        var group = Foundation.NSFileManager.DefaultManager.GetContainerUrl("group.com.emir.cepbankapp.cepbankapp");
        if (group?.Path is not string directory) return;

        await _writeGate.WaitAsync();
        try
        {
            var events = (await notes.GetRemindersAsync())
                .Select(x => new { startDate = x.EventDate.ToString("yyyy-MM-dd"), endDate = x.LastDate.ToString("yyyy-MM-dd"), category = (int)x.Category, name = x.Name, startTime = x.StartTime })
                .ToList();
            var snapshot = new
            {
                events,
                updatedAt = DateTimeOffset.Now.ToString("O")
            };
            var path = Path.Combine(directory, "widget-summary.json");
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot));
            File.Move(temporary, path, true);
        }
        finally { _writeGate.Release(); }
#else
        await Task.CompletedTask;
#endif
    }
}
