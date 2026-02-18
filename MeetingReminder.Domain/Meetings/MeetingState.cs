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
    public MeetingEvent Event { get; init; }

    /// <summary>
    /// Current notification level for this meeting
    /// </summary>
    public NotificationLevel CurrentLevel { get; private set; }

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
        IsAcknowledged = false;
        LastNotificationTime = DateTime.MinValue;
    }

    /// <summary>
    /// Updates the notification level for this meeting.
    /// Only allows escalation - notification level can only increase, never decrease.
    /// </summary>
    /// <param name="level">The new notification level</param>
    public void UpdateNotificationLevel(NotificationLevel level)
    {
        // Only allow escalation - notification level can only increase
        if (level > CurrentLevel)
        {
            CurrentLevel = level;
        }
    }

    /// <summary>
    /// Marks this meeting as acknowledged by the user.
    /// Resets notification level to None and sets acknowledged flag.
    /// </summary>
    public void Acknowledge()
    {
        IsAcknowledged = true;
        CurrentLevel = NotificationLevel.None;
    }

    /// <summary>
    /// Updates the timestamp of the last notification sent.
    /// </summary>
    /// <param name="timestamp">The timestamp to record</param>
    public void UpdateLastNotificationTime(DateTime timestamp)
    {
        LastNotificationTime = timestamp;
    }
}
