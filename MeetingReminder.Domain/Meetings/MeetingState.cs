using MeetingReminder.Domain.Notifications;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Entity that tracks the notification state of a meeting.
/// Manages notification level escalation and acknowledgement status.
/// </summary>
public readonly record struct MeetingState(MeetingEvent Event, NotificationLevel CurrentLevel, NotificationLevel PreviousLevel, bool IsAcknowledged, DateTime LastNotificationTime)
{
    public static MeetingState New(MeetingEvent meetingEvent)
        => new MeetingState(NotNull(meetingEvent), NotificationLevel.None, NotificationLevel.None, false, DateTime.MinValue);

    /// <summary>
    /// Updates the notification level for this meeting.
    /// Only allows escalation - notification level can only increase, never decrease.
    /// </summary>
    /// <param name="level">The new notification level</param>
    /// <returns>True if the level was actually changed (escalated), false otherwise</returns>
    public MeetingState UpdateNotificationLevel(NotificationLevel level, DateTime timestamp)
    {
        // Only allow escalation - notification level can only increase
        if (level > CurrentLevel)
            return this with { CurrentLevel = level, PreviousLevel = CurrentLevel, LastNotificationTime = timestamp };

        if (level == CurrentLevel)
            return this with { PreviousLevel = CurrentLevel };

        return this;
    }

    /// <summary>
    /// Marks this meeting as acknowledged by the user.
    /// Resets notification level to None and sets acknowledged flag.
    /// </summary>
    public MeetingState Acknowledge(DateTime timestamp)
        => this with
        {
            IsAcknowledged = true,
            CurrentLevel = NotificationLevel.None,
            PreviousLevel = NotificationLevel.None,
            LastNotificationTime = timestamp
        };

    public MeetingState UpdateEvent(MeetingEvent meeting)
        => this with { Event = NotNull(meeting) };

    public bool NotificationLevelHasChanged => CurrentLevel > PreviousLevel;
}
