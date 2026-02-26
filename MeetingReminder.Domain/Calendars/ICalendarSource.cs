namespace MeetingReminder.Domain.Calendars;

/// <summary>
/// Abstraction for calendar data sources.
/// Implementations handle the specifics of fetching events from different calendar providers
/// (Google Calendar, iCal/ICS files, etc.).
/// Returns raw calendar events without any enrichment (link extraction, etc.).
/// </summary>
public interface ICalendarSource
{
    /// <summary>
    /// Fetches raw calendar events within the specified time range.
    /// </summary>
    /// <param name="startTime">Start of the time range to fetch events for</param>
    /// <param name="endTime">End of the time range to fetch events for</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>Result containing the list of raw calendar events or a CalendarError</returns>
    Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the name of this calendar source for identification and logging purposes.
    /// </summary>
    string Name { get; }
}
