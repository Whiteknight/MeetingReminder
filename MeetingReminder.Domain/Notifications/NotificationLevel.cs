namespace MeetingReminder.Domain.Notifications;

/// <summary>
/// Represents the urgency level of a meeting notification.
/// Levels escalate from None to Critical as the meeting time approaches.
/// </summary>
public enum NotificationLevel
{
    /// <summary>
    /// No notification - meeting is too far away or outside notification window
    /// </summary>
    None = 0,

    /// <summary>
    /// Gentle notification - early warning for upcoming meeting
    /// </summary>
    Gentle = 1,

    /// <summary>
    /// Moderate notification - meeting is getting closer
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Urgent notification - meeting is very close
    /// </summary>
    Urgent = 3,

    /// <summary>
    /// Critical notification - meeting has started or is past start time
    /// </summary>
    Critical = 4
}
