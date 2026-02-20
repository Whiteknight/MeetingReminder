using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Query to calculate the notification level for a meeting based on current time and configuration.
/// </summary>
/// <param name="Meeting">The meeting event to calculate notification level for</param>
/// <param name="CurrentTime">The current time to calculate against</param>
/// <param name="Thresholds">Notification timing thresholds</param>
/// <param name="Rules">Optional per-calendar notification rules (null uses default behavior)</param>
public readonly record struct CalculateNotificationLevelQuery(
    MeetingEvent Meeting,
    DateTime CurrentTime,
    INotificationThresholds Thresholds,
    ICalendarNotificationRules? Rules = null);

/// <summary>
/// Calculates the notification level for a meeting.
/// Implements threshold-based escalation with support for all-day event suppression
/// and per-calendar notification time windows.
/// </summary>
public class CalculateNotificationLevel
{
    /// <summary>
    /// Calculates the notification level for a meeting based on time until start
    /// and configured thresholds.
    /// </summary>
    /// <param name="query">The query containing meeting, current time, and configuration</param>
    /// <returns>Result containing the calculated NotificationLevel or an error</returns>
    public Result<NotificationLevel, NotificationError> Calculate(CalculateNotificationLevelQuery query)
    {
        var meeting = query.Meeting;
        var currentTime = query.CurrentTime;
        var thresholds = query.Thresholds;
        var rules = query.Rules;

        // All-day events never escalate (Requirements 8.6, 8.7)
        if (meeting.IsAllDay)
            return NotificationLevel.None;

        // Check if current time is within notification window (Requirements 10.8, 10.9)
        if (!IsWithinNotificationWindow(rules, currentTime))
            return NotificationLevel.None;

        return CalculateLevelFromTimeUntilStart(meeting, currentTime, thresholds);
    }

    // If no rules configured, notifications are always active
    private static bool IsWithinNotificationWindow(
        ICalendarNotificationRules? rules,
        DateTime currentTime)
        => rules is null || rules.IsWithinNotificationWindow(currentTime);

    private static NotificationLevel CalculateLevelFromTimeUntilStart(
        MeetingEvent meeting,
        DateTime currentTime,
        INotificationThresholds thresholds)
    {
        var timeUntilStart = meeting.GetTimeUntilStart(currentTime);

        // At or past meeting start time = Critical (Requirement 8.3)
        if (timeUntilStart <= TimeSpan.Zero)
            return NotificationLevel.Critical;

        // Within urgent threshold (Requirement 8.2)
        if (timeUntilStart <= thresholds.UrgentMinutes)
            return NotificationLevel.Urgent;

        // Within moderate threshold (Requirement 8.2)
        if (timeUntilStart <= thresholds.ModerateMinutes)
            return NotificationLevel.Moderate;

        // Within gentle threshold (Requirement 8.1)
        if (timeUntilStart <= thresholds.GentleMinutes)
            return NotificationLevel.Gentle;

        // Meeting is too far away for notifications
        return NotificationLevel.None;
    }
}
