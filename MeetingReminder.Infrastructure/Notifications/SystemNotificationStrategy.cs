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
    public Task<Result<NotificationLevel, NotificationError>> ExecuteOnCycleAsync(IReadOnlyList<MeetingState> meetings)
    {
        // Toast notifications only appear on level change, not every cycle
        return Task.FromResult<Result<NotificationLevel, NotificationError>>(NotificationLevel.None);
    }

    /// <summary>
    /// Shows a system notification when the notification level escalates.
    /// </summary>
    public async Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(MeetingState meeting)
    {
        if (!IsSupported)
            return new NotificationError("System notifications are not supported on this platform", StrategyName);

        if (meeting.CurrentLevel == NotificationLevel.None)
            return Unit.Value;

        try
        {
            var title = GetNotificationTitle(meeting);
            var body = GetNotificationBody(meeting);

            await _provider.ShowNotificationAsync(title, body, meeting.CurrentLevel);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to show system notification: {ex.Message}", StrategyName);
        }
    }

    private static string GetNotificationTitle(MeetingState meeting)
    {
        var prefix = meeting.CurrentLevel switch
        {
            NotificationLevel.Gentle => "📅 Upcoming",
            NotificationLevel.Moderate => "⏰ Soon",
            NotificationLevel.Urgent => "⚠️ Starting Soon",
            NotificationLevel.Critical => "🚨 NOW",
            _ => "📅"
        };

        return $"{prefix}: {meeting.Event.Title}";
    }

    private static string GetNotificationBody(MeetingState meeting)
    {
        var timeInfo = meeting.CurrentLevel == NotificationLevel.Critical
            ? "Meeting has started!"
            : $"Starts at {meeting.Event.StartTime:HH:mm}";

        var locationInfo = string.IsNullOrEmpty(meeting.Event.Location)
            ? string.Empty
            : $"\n📍 {meeting.Event.Location}";

        var linkInfo = meeting.Event.Link != null
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
