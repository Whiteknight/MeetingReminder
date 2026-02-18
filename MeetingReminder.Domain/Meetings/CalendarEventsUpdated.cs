namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Domain event published when calendar events are fetched and changes are detected.
/// Contains all current events, newly added events, and IDs of removed events.
/// </summary>
public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    IReadOnlyList<MeetingEvent> AddedEvents,
    IReadOnlyList<string> RemovedEventIds,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
