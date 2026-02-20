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
    public Task<Result<Unit, NotificationError>> ExecuteOnCycleAsync(NotificationLevel level, MeetingEvent meeting)
    {
        return Task.FromResult(Execute(level));
    }

    /// <summary>
    /// Beeps don't need special handling on level change - they execute every cycle.
    /// </summary>
    public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(
        NotificationLevel previousLevel,
        NotificationLevel newLevel,
        MeetingEvent meeting)
    {
        // Beeps execute on every cycle, not just on level change
        return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
    }

    private Result<Unit, NotificationError> Execute(NotificationLevel level)
    {
        if (!IsSupported)
        {
            return new NotificationError("Beep notification is not supported on this platform", StrategyName);
        }

        if (level == NotificationLevel.None)
        {
            return Unit.Value;
        }

        try
        {
            ExecuteBeepPattern(level);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to execute beep notification: {ex.Message}", StrategyName);
        }
    }

    private static void ExecuteBeepPattern(NotificationLevel level)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var (frequency, duration, repetitions) = GetBeepParameters(level);

        for (int i = 0; i < repetitions; i++)
        {
            Console.Beep(frequency, duration);
            if (i < repetitions - 1)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static (int Frequency, int Duration, int Repetitions) GetBeepParameters(NotificationLevel level)
    {
        // Frequencies bumped up for better audibility on modern systems
        return level switch
        {
            NotificationLevel.Gentle => (800, 300, 1),
            NotificationLevel.Moderate => (1000, 400, 2),
            NotificationLevel.Urgent => (1200, 500, 3),
            NotificationLevel.Critical => (1500, 600, 4),
            _ => (800, 300, 1)
        };
    }
}
