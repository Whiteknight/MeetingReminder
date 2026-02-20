using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Domain.Notifications;

/// <summary>
/// Message published when notification states change for meetings.
/// Contains all meetings that currently have active notifications.
/// Used for channel-based communication between threads.
/// </summary>
public record NotificationStateChanged(
    IReadOnlyList<MeetingState> ActiveNotifications,
    DateTime OccurredAt);
