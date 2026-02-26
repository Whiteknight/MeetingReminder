using MeetingReminder.Domain.Notifications;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Entity that tracks the notification state of a meeting.
/// Manages notification level escalation and acknowledgement status.
/// </summary>
public class MeetingState
{
    /// <summary>
    /// The meeting event this state tracks
    /// </summary>
    public MeetingEvent Event { get; set; }

    /// <summary>
    /// Current notification level for this meeting
    /// </summary>
    public NotificationLevel CurrentLevel { get; private set; }

    public NotificationLevel PreviousLevel { get; private set; }

    /// <summary>
    /// Indicates whether the user has acknowledged this meeting
    /// </summary>
    public bool IsAcknowledged { get; private set; }

    /// <summary>
    /// Timestamp of the last notification sent for this meeting
    /// </summary>
    public DateTime LastNotificationTime { get; private set; }

    public MeetingState(MeetingEvent meetingEvent)
    {
        Event = NotNull(meetingEvent);
        CurrentLevel = NotificationLevel.None;
        PreviousLevel = NotificationLevel.None;
        IsAcknowledged = false;
        LastNotificationTime = DateTime.MinValue;
    }

    /// <summary>
    /// Updates the notification level for this meeting.
    /// Only allows escalation - notification level can only increase, never decrease.
    /// </summary>
    /// <param name="level">The new notification level</param>
    /// <returns>True if the level was actually changed (escalated), false otherwise</returns>
    public bool UpdateNotificationLevel(NotificationLevel level)
    {
        // Only allow escalation - notification level can only increase
        if (level > CurrentLevel)
        {
            PreviousLevel = CurrentLevel;
            CurrentLevel = level;
            return true;
        }
        if (level == CurrentLevel)
            PreviousLevel = CurrentLevel;
        return false;
    }

    /// <summary>
    /// Marks this meeting as acknowledged by the user.
    /// Resets notification level to None and sets acknowledged flag.
    /// </summary>
    public void Acknowledge()
    {
        IsAcknowledged = true;
        CurrentLevel = NotificationLevel.None;
        PreviousLevel = NotificationLevel.None;
    }

    /// <summary>
    /// Updates the timestamp of the last notification sent.
    /// </summary>
    /// <param name="timestamp">The timestamp to record</param>
    public void UpdateLastNotificationTime(DateTime timestamp)
    {
        LastNotificationTime = timestamp;
    }

    public bool NotificationLevelHasChanged => CurrentLevel > PreviousLevel;
}
