using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Configuration for notification escalation timing thresholds.
/// Defines when notifications transition between urgency levels based on time until meeting start.
/// </summary>
/// <param name="GentleMinutes">Time threshold for gentle notifications (default: 10 minutes)</param>
/// <param name="ModerateMinutes">Time threshold for moderate notifications (default: 5 minutes)</param>
/// <param name="UrgentMinutes">Time threshold for urgent notifications (default: 1 minute)</param>
public record NotificationThresholds(
    TimeSpan GentleMinutes,
    TimeSpan ModerateMinutes,
    TimeSpan UrgentMinutes) : INotificationThresholds
{
    /// <summary>
    /// Default notification thresholds:
    /// - Gentle: 10 minutes before meeting
    /// - Moderate: 5 minutes before meeting
    /// - Urgent: 1 minute before meeting
    /// - Critical: At or past meeting start time (implicit)
    /// </summary>
    public static NotificationThresholds Default
        => new(
            GentleMinutes: TimeSpan.FromMinutes(10),
            ModerateMinutes: TimeSpan.FromMinutes(5),
            UrgentMinutes: TimeSpan.FromMinutes(1));

    /// <summary>
    /// Validates that the thresholds are in the correct order (Gentle > Moderate > Urgent > 0).
    /// </summary>
    /// <returns>True if thresholds are valid, false otherwise</returns>
    public bool IsValid()
        => GentleMinutes > ModerateMinutes
            && ModerateMinutes > UrgentMinutes
            && UrgentMinutes > TimeSpan.Zero;
}
