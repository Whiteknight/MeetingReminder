using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Query to fetch calendar events from multiple calendar sources within a time range.
/// </summary>
/// <param name="StartTime">Start of the time range to fetch events for</param>
/// <param name="EndTime">End of the time range to fetch events for</param>
public readonly record struct FetchCalendarEventsQuery(DateTime StartTime, DateTime EndTime);

/// <summary>
/// Fetches calendar events from multiple calendar sources.
/// Fetches raw events from all sources concurrently, aggregates results,
/// and enriches them with extracted meeting links.
/// Succeeds if at least one source returns events successfully.
/// </summary>
public class FetchCalendarEvents
{
    private readonly IEnumerable<ICalendarSource> _sources;
    private readonly ExtractMeetingLink _linkExtractor;

    public FetchCalendarEvents(IEnumerable<ICalendarSource> sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        // TODO: Inject this
        _linkExtractor = new ExtractMeetingLink();
    }

    /// <summary>
    /// Fetches calendar events from all configured sources concurrently.
    /// </summary>
    /// <param name="query">The query containing the time range</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>
    /// Result containing aggregated and enriched meeting events from all successful sources,
    /// or a CalendarError if all sources failed.
    /// </returns>
    public async Task<Result<IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>>, CalendarError>> Fetch(
        FetchCalendarEventsQuery query,
        CancellationToken cancellationToken)
    {
        var sourceList = _sources.ToList();

        if (sourceList.Count == 0)
            return CalendarError.NoSourcesConfigured();

        var fetchTasks = sourceList
            .Select(source =>
                FetchFromSource(source, query, cancellationToken));

        var results = await Task.WhenAll(fetchTasks);

        return AggregateAndEnrichResults(results);
    }

    private static async Task<SourceFetchResult> FetchFromSource(
        ICalendarSource source,
        FetchCalendarEventsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await source.FetchEvents(
            query.StartTime,
            query.EndTime,
            cancellationToken);

        return result.Match(
            events => new SourceFetchResult(source.Name, events, null),
            error => new SourceFetchResult(source.Name, null, error));
    }

    private Result<IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>>, CalendarError> AggregateAndEnrichResults(
        SourceFetchResult[] results)
    {
        var eventsByCalendar = new Dictionary<CalendarName, IReadOnlyList<MeetingEvent>>();
        var errors = new List<CalendarError>();

        foreach (var result in results)
        {
            if (result.Events is not null)
            {
                // Include the calendar even when the event list is empty so that
                // ConsolidateIncomingMeetings can remove stale entries for that calendar.
                eventsByCalendar[result.SourceName] = result.Events
                    .Select(EnrichRawEvent)
                    .ToList()
                    .AsReadOnly();
            }
            else if (result.Error is not null)
            {
                errors.Add(result.Error);
            }
        }

        // Succeed if at least one source responded (even with an empty list)
        // TODO: This case is probably always true, since it would only be 0 when there are no
        // configured sources but that case has already been filtered out.
        if (eventsByCalendar.Count > 0)
            return eventsByCalendar;

        // All sources errored
        if (errors.Count == 0)
            return CalendarError.NoEventsFound();

        if (errors.Count == 1)
            return errors[0];

        var errorMessages = string.Join("; ", errors.Select(e => $"{e.CalendarSource}: {e.Message}"));
        return CalendarError.AllSourcesFailed(errors.Count, errorMessages);
    }

    private MeetingEvent EnrichRawEvent(RawCalendarEvent raw)
    {
        var linkQuery = new ExtractMeetingLinkQuery(raw.Description, raw.Location);
        var link = _linkExtractor.Extract(linkQuery)
            .Match(l => (MeetingLink?)l, _ => null);

        return MeetingEvent.Create(
            id: new MeetingId(raw.Calendar, raw.Id),
            title: raw.Title,
            startTime: raw.StartTime,
            endTime: raw.EndTime,
            description: raw.Description,
            location: raw.Location,
            isAllDay: raw.IsAllDay,
            calendar: raw.Calendar,
            link: link);
    }

    private record SourceFetchResult(
        CalendarName SourceName,
        IReadOnlyList<RawCalendarEvent>? Events,
        CalendarError? Error);
}
