namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Domain event published when a user acknowledges a meeting.
/// Indicates whether the meeting link was opened as part of the acknowledgement.
/// </summary>
public record MeetingAcknowledged(
    string MeetingId,
    bool LinkOpened,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
