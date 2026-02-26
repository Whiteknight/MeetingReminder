using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Infrastructure.Windows.Notifications;

/// <summary>
/// Notification strategy that uses console beeps with varying frequency and duration
/// based on the notification urgency level. Windows only.
/// Beeps execute on every polling cycle to provide persistent audio reminders.
/// </summary>
public class BeepNotificationStrategy : INotificationStrategy
{
    public string StrategyName => "Beep";

    public bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Executes beep on every polling cycle for persistent audio reminders.
    /// </summary>
    public async Task<Result<NotificationLevel, NotificationError>> ExecuteOnCycleAsync(IReadOnlyList<MeetingState> meetings)
    {
        if (!IsSupported)
            return new NotificationError("Beep notification is not supported on this platform", StrategyName);

        var level = meetings.Max(m => m.CurrentLevel);
        if (level == NotificationLevel.None)
            return NotificationLevel.None;

        try
        {
            var (frequency, duration, repetitions) = GetBeepParameters(level);
            for (int i = 0; i < repetitions; i++)
            {
                Console.Beep(frequency, duration);
                if (i < repetitions - 1)
                    await Task.Delay(100);
            }

            return level;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to execute beep notification: {ex.Message}", StrategyName);
        }
    }

    /// <summary>
    /// Beeps don't need special handling on level change - they execute every cycle.
    /// </summary>
    public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(MeetingState meeting)
    {
        // Beeps execute on every cycle, not just on level change
        return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
    }

    // Frequencies bumped up for better audibility on modern systems
    private static (int Frequency, int Duration, int Repetitions) GetBeepParameters(NotificationLevel level)
        => level switch
        {
            NotificationLevel.Gentle => (800, 300, 1),
            NotificationLevel.Moderate => (1000, 400, 2),
            NotificationLevel.Urgent => (1200, 500, 3),
            NotificationLevel.Critical => (1500, 600, 4),
            _ => (800, 300, 1)
        };
}
