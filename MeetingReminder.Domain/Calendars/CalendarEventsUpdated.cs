using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Domain.Calendars;

/// <summary>
/// Message published when calendar events are fetched and changes are detected.
/// Contains all current events, newly added events, and IDs of removed events.
/// Used for channel-based communication between threads.
/// </summary>
public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    IReadOnlyList<MeetingEvent> AddedEvents,
    IReadOnlyList<string> RemovedEventIds,
    DateTime OccurredAt);
