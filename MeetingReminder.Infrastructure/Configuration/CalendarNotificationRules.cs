using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Per-calendar notification rules that control when and how notifications are triggered
/// for meetings from a specific calendar source.
/// </summary>
/// <param name="NotificationWindowStart">Start of daily notification window (null = no restriction)</param>
/// <param name="NotificationWindowEnd">End of daily notification window (null = no restriction)</param>
/// <param name="UrgencyMultiplier">Multiplier for urgency calculation (higher = more urgent)</param>
public record CalendarNotificationRules(
    TimeSpan? NotificationWindowStart,
    TimeSpan? NotificationWindowEnd,
    int UrgencyMultiplier) : ICalendarNotificationRules
{
    /// <summary>
    /// Default rules with no time window restrictions and standard urgency.
    /// </summary>
    public static CalendarNotificationRules Default => new(
        NotificationWindowStart: null,
        NotificationWindowEnd: null,
        UrgencyMultiplier: 1);

    /// <summary>
    /// Determines if the given time falls within the configured notification window.
    /// If no window is configured (both start and end are null), always returns true.
    /// </summary>
    /// <param name="currentTime">The current time to check</param>
    /// <returns>True if notifications should be active at this time</returns>
    public bool IsWithinNotificationWindow(DateTime currentTime)
    {
        // If no window is configured, notifications are always active
        if (NotificationWindowStart is null || NotificationWindowEnd is null)
            return true;

        var timeOfDay = currentTime.TimeOfDay;

        // Handle normal case where start < end (e.g., 9:00 AM to 5:00 PM)
        if (NotificationWindowStart.Value <= NotificationWindowEnd.Value)
        {
            return timeOfDay >= NotificationWindowStart.Value
                && timeOfDay <= NotificationWindowEnd.Value;
        }

        // Handle overnight case where start > end (e.g., 10:00 PM to 6:00 AM)
        return timeOfDay >= NotificationWindowStart.Value
            || timeOfDay <= NotificationWindowEnd.Value;
    }

    /// <summary>
    /// Validates that the rules are properly configured.
    /// </summary>
    /// <returns>True if rules are valid</returns>
    public bool IsValid()
    {
        // UrgencyMultiplier must be positive
        if (UrgencyMultiplier <= 0)
            return false;

        // If one window bound is set, both must be set
        if ((NotificationWindowStart is null) != (NotificationWindowEnd is null))
            return false;

        // Time of day values must be valid (0-24 hours)
        if (NotificationWindowStart is not null && 
            (NotificationWindowStart.Value < TimeSpan.Zero || NotificationWindowStart.Value >= TimeSpan.FromHours(24)))
            return false;

        if (NotificationWindowEnd is not null && 
            (NotificationWindowEnd.Value < TimeSpan.Zero || NotificationWindowEnd.Value >= TimeSpan.FromHours(24)))
            return false;

        return true;
    }
}
