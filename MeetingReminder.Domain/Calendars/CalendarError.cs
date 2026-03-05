namespace MeetingReminder.Domain.Calendars;

public sealed record CalendarError(
    string Message,
    CalendarName CalendarSource,
    Exception? InnerException = null) : Error(Message)
{
    public static CalendarError RequestCancelled(CalendarName calendarSource, Exception? ex = null)
        => new("Request was cancelled", calendarSource, ex);

    public static CalendarError RequestTimedOut(CalendarName calendarSource, Exception? ex = null)
        => new("Request timed out", calendarSource, ex);

    public static CalendarError NetworkError(CalendarName calendarSource, string message, Exception? ex = null)
        => new($"Network error fetching iCal data: {message}", calendarSource, ex);

    public static CalendarError HttpError(CalendarName calendarSource, int statusCode, string? reasonPhrase)
        => new($"Failed to fetch iCal data: HTTP {statusCode} {reasonPhrase}", calendarSource);

    public static CalendarError EmptyData(CalendarName calendarSource)
        => new("iCal data is empty", calendarSource);

    public static CalendarError ParseError(CalendarName calendarSource, string message, Exception? ex = null)
        => new($"Failed to parse iCal data: {message}", calendarSource, ex);

    public static CalendarError MappingError(CalendarName calendarSource, string message, Exception? ex = null)
        => new($"Failed to map calendar event: {message}", calendarSource, ex);

    public static CalendarError NoSourcesConfigured()
        => new("No calendar sources configured", default);

    public static CalendarError AllSourcesFailed(int errorCount, string errorMessages)
        => new($"All {errorCount} calendar sources failed: {errorMessages}", new CalendarName("aggregate"));

    public static CalendarError NoEventsFound()
        => new("No events found from any calendar source", default);
}
