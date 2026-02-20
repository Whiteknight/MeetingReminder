using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Domain.Notifications;

/// <summary>
/// Abstraction for notification strategies that alert users about upcoming meetings.
/// Each strategy represents a different method of notification (beep, flash, sound, etc.).
/// Strategies can execute on every polling cycle, on level changes, or both.
/// </summary>
public interface INotificationStrategy
{
    /// <summary>
    /// Executes the notification strategy on every polling cycle.
    /// Use this for notifications that should repeat frequently (e.g., beeps, sounds).
    /// Return success with no action if this strategy doesn't need per-cycle execution.
    /// </summary>
    /// <param name="level">The notification urgency level determining intensity</param>
    /// <param name="meeting">The meeting event to notify about</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<Result<Unit, NotificationError>> ExecuteOnCycleAsync(NotificationLevel level, MeetingEvent meeting);

    /// <summary>
    /// Executes the notification strategy when the notification level changes (escalates).
    /// Use this for notifications that should only appear once per level change (e.g., toast notifications).
    /// Return success with no action if this strategy doesn't need level-change execution.
    /// </summary>
    /// <param name="previousLevel">The previous notification level</param>
    /// <param name="newLevel">The new (escalated) notification level</param>
    /// <param name="meeting">The meeting event to notify about</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(
        NotificationLevel previousLevel,
        NotificationLevel newLevel,
        MeetingEvent meeting);

    /// <summary>
    /// Gets the unique name identifying this notification strategy.
    /// Used for configuration and logging purposes.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Indicates whether this strategy is supported on the current platform.
    /// Strategies should check platform compatibility and return false if not supported.
    /// </summary>
    bool IsSupported { get; }
}
