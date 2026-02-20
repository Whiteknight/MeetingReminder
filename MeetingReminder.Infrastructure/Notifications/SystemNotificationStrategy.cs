using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Infrastructure.Notifications;

/// <summary>
/// Notification strategy that uses the operating system's native notification system.
/// Requires a platform-specific ISystemNotificationProvider to be injected.
/// Toast notifications only appear on level changes to avoid notification spam.
/// </summary>
public class SystemNotificationStrategy : INotificationStrategy
{
    private readonly ISystemNotificationProvider _provider;

    public SystemNotificationStrategy(ISystemNotificationProvider provider)
    {
        _provider = provider;
    }

    public string StrategyName => "SystemNotification";

    public bool IsSupported => _provider.IsSupported;

    /// <summary>
    /// System notifications don't execute on every cycle to avoid notification spam.
    /// </summary>
    public Task<Result<Unit, NotificationError>> ExecuteOnCycleAsync(NotificationLevel level, MeetingEvent meeting)
    {
        // Toast notifications only appear on level change, not every cycle
        return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
    }

    /// <summary>
    /// Shows a system notification when the notification level escalates.
    /// </summary>
    public async Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(
        NotificationLevel previousLevel,
        NotificationLevel newLevel,
        MeetingEvent meeting)
    {
        if (!IsSupported)
        {
            return new NotificationError("System notifications are not supported on this platform", StrategyName);
        }

        if (newLevel == NotificationLevel.None)
        {
            return Unit.Value;
        }

        try
        {
            var title = GetNotificationTitle(newLevel, meeting);
            var body = GetNotificationBody(newLevel, meeting);

            await _provider.ShowNotificationAsync(title, body, newLevel);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to show system notification: {ex.Message}", StrategyName);
        }
    }

    private static string GetNotificationTitle(NotificationLevel level, MeetingEvent meeting)
    {
        var prefix = level switch
        {
            NotificationLevel.Gentle => "📅 Upcoming",
            NotificationLevel.Moderate => "⏰ Soon",
            NotificationLevel.Urgent => "⚠️ Starting Soon",
            NotificationLevel.Critical => "🚨 NOW",
            _ => "📅"
        };

        return $"{prefix}: {meeting.Title}";
    }

    private static string GetNotificationBody(NotificationLevel level, MeetingEvent meeting)
    {
        var timeInfo = level == NotificationLevel.Critical
            ? "Meeting has started!"
            : $"Starts at {meeting.StartTime:HH:mm}";

        var locationInfo = string.IsNullOrEmpty(meeting.Location)
            ? string.Empty
            : $"\n📍 {meeting.Location}";

        var linkInfo = meeting.Link != null
            ? "\n🔗 Meeting link available"
            : string.Empty;

        return $"{timeInfo}{locationInfo}{linkInfo}";
    }
}

/// <summary>
/// Abstraction for platform-specific system notification implementations.
/// </summary>
public interface ISystemNotificationProvider
{
    bool IsSupported { get; }
    Task ShowNotificationAsync(string title, string body, NotificationLevel level);
}
