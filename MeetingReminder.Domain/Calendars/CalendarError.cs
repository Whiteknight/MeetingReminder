namespace MeetingReminder.Domain.Calendars;

public sealed record CalendarError(
    string Message,
    string? CalendarSource = null,
    Exception? InnerException = null) : Error(Message)
{
    public static CalendarError RequestCancelled(string calendarSource, Exception? ex = null)
        => new("Request was cancelled", calendarSource, ex);

    public static CalendarError RequestTimedOut(string calendarSource, Exception? ex = null)
        => new("Request timed out", calendarSource, ex);

    public static CalendarError NetworkError(string calendarSource, string message, Exception? ex = null)
        => new($"Network error fetching iCal data: {message}", calendarSource, ex);

    public static CalendarError HttpError(string calendarSource, int statusCode, string? reasonPhrase)
        => new($"Failed to fetch iCal data: HTTP {statusCode} {reasonPhrase}", calendarSource);

    public static CalendarError EmptyData(string calendarSource)
        => new("iCal data is empty", calendarSource);

    public static CalendarError ParseError(string calendarSource, string message, Exception? ex = null)
        => new($"Failed to parse iCal data: {message}", calendarSource, ex);

    public static CalendarError MappingError(string calendarSource, string message, Exception? ex = null)
        => new($"Failed to map calendar event: {message}", calendarSource, ex);

    public static CalendarError NoSourcesConfigured()
        => new("No calendar sources configured");

    public static CalendarError AllSourcesFailed(int errorCount, string errorMessages)
        => new($"All {errorCount} calendar sources failed: {errorMessages}", "aggregate");

    public static CalendarError NoEventsFound()
        => new("No events found from any calendar source");
}
