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
    /// Use this for notifications that should repeat frequently but which don't need to be executed per meeting (e.g., beeps, sounds).
    /// Return success with no action if this strategy doesn't need per-cycle execution.
    /// </summary>
    /// <param name="meetings">The meeting event to notify about</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<Result<NotificationLevel, NotificationError>> ExecuteOnCycleAsync(IReadOnlyList<MeetingState> meetings);

    /// <summary>
    /// Executes the notification strategy when the notification level changes (escalates).
    /// Use this for notifications that should only appear once per level change (e.g., toast notifications).
    /// Return success with no action if this strategy doesn't need level-change execution.
    /// </summary>
    /// <param name="meeting">The meeting event to notify about</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(MeetingState meeting);

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
