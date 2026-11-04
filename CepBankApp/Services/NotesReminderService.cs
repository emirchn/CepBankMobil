using CepBankApp.Data;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace CepBankApp.Services;

public sealed class NotesReminderService(NotesStore store)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public static readonly int[] Days = NotificationSettings.ReminderDays;
    public const int FirstId = 1_200_000_000;
    public const int LastId = 1_400_000_000;
    public static bool IsNotesId(int id) => id >= FirstId && id < LastId;

    public static int DeliveryHour
    {
        get => Preferences.Default.Get("notes_notify_hour", 9);
        set => Preferences.Default.Set("notes_notify_hour", value);
    }

    public static int ReminderDaysMask
    {
        get => Preferences.Default.Get("notes_notify_days", 1 << 3);
        set => Preferences.Default.Set("notes_notify_days", value);
    }

    public static bool HasDay(int mask, int day) => (mask & (1 << day)) != 0;

    public async Task RefreshAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var pending = await LocalNotificationCenter.Current.GetPendingNotificationList();
            var ownIds = pending.Select(x => x.NotificationId).Where(IsNotesId).ToArray();
            if (ownIds.Length > 0) LocalNotificationCenter.Current.Cancel(ownIds);
            if (!await LocalNotificationCenter.Current.AreNotificationsEnabled(new NotificationPermission { AskPermission = false })) return;
            var now = DateTime.Now;
            var candidates = (await store.GetRemindersAsync())
                .SelectMany(item => Days.Where(day => HasDay(ReminderDaysMask, day))
                    .Select(day => (item, day, when: item.EventDate.Date.AddDays(-day).AddHours(DeliveryHour))))
                .Where(x => x.when > now)
                .OrderBy(x => x.when)
                .Take(24)
                .ToList();
            foreach (var candidate in candidates)
            {
                // IDs are scoped to this feature and stable across rescheduling.
                var id = FirstId + candidate.item.Id * 16 + candidate.day;
                if (id >= LastId) continue;
                await LocalNotificationCenter.Current.Show(new NotificationRequest
                {
                    NotificationId = id,
                    Title = "Yaklaşan etkinlik: " + candidate.item.Name,
                    Description = $"{candidate.item.EventDate:dd.MM.yyyy} tarihine {candidate.day} gün kaldı.",
                    Schedule = new NotificationRequestSchedule { NotifyTime = candidate.when }
                });
            }
        }
        finally { _gate.Release(); }
    }

    public async Task RequestPermissionAndRefreshAsync()
    {
        await LocalNotificationCenter.Current.RequestNotificationPermission(new NotificationPermission { AskPermission = true });
        await RefreshAsync();
    }
}
