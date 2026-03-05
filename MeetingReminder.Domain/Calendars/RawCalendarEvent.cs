namespace MeetingReminder.Domain.Calendars;

/// <summary>
/// Raw calendar event data as retrieved from a calendar source.
/// This is a DTO-style model containing only the data as it exists in the source,
/// without any computed or extracted values.
/// </summary>
/// <param name="Id">Unique identifier for the event</param>
/// <param name="Title">Event title/summary</param>
/// <param name="StartTime">Event start date and time</param>
/// <param name="EndTime">Event end date and time</param>
/// <param name="Description">Event description/body text</param>
/// <param name="Location">Event location (physical or virtual)</param>
/// <param name="IsAllDay">Whether this is an all-day event</param>
/// <param name="Calendar">Name of the calendar source this event came from</param>
public record RawCalendarEvent(
    string Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Description,
    string Location,
    bool IsAllDay,
    CalendarName Calendar);
