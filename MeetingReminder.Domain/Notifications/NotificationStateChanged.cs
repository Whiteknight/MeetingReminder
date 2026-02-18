using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Domain.Notifications;

/// <summary>
/// Domain event published when notification states change for meetings.
/// Contains all meetings that currently have active notifications.
/// </summary>
public record NotificationStateChanged(
    IReadOnlyList<MeetingState> ActiveNotifications,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
