namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Message published when a user acknowledges a meeting.
/// Indicates whether the meeting link was opened as part of the acknowledgement.
/// Used for channel-based communication between threads.
/// </summary>
public record MeetingAcknowledged(
    string MeetingId,
    bool LinkOpened,
    DateTime OccurredAt);
