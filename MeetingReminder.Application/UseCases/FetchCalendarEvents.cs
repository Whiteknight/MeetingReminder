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
    public async Task<Result<IReadOnlyDictionary<string, IReadOnlyList<MeetingEvent>>, CalendarError>> Fetch(
        FetchCalendarEventsQuery query,
        CancellationToken cancellationToken)
    {
        var sourceList = _sources.ToList();

        if (sourceList.Count == 0)
            return CalendarError.NoSourcesConfigured();

        var fetchTasks = sourceList.Select(source =>
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
            events => new SourceFetchResult(source.SourceName, events, null),
            error => new SourceFetchResult(source.SourceName, null, error));
    }

    private Result<IReadOnlyDictionary<string, IReadOnlyList<MeetingEvent>>, CalendarError> AggregateAndEnrichResults(
        SourceFetchResult[] results)
    {
        var allRawEvents = new List<RawCalendarEvent>();
        var errors = new List<CalendarError>();

        foreach (var result in results)
        {
            if (result.Events is not null)
                allRawEvents.AddRange(result.Events);
            else if (result.Error is not null)
                errors.Add(result.Error);
        }

        // Succeed if at least one source returned events
        if (allRawEvents.Count > 0)
        {
            var enrichedEvents = allRawEvents
                .Select(EnrichRawEvent)
                .ToList()
                .AsReadOnly();
            return enrichedEvents.GroupBy(e => e.CalendarSource).ToDictionary(e => e.Key, e => (IReadOnlyList<MeetingEvent>)e.ToList());
        }

        // All sources failed - return aggregate error
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
        var linkResult = _linkExtractor.Extract(linkQuery);
        var link = linkResult.Match(l => (MeetingLink?)l, _ => null);

        return new MeetingEvent(
            id: raw.Id,
            title: raw.Title,
            startTime: raw.StartTime,
            endTime: raw.EndTime,
            description: raw.Description,
            location: raw.Location,
            isAllDay: raw.IsAllDay,
            calendarSource: raw.CalendarSource,
            link: link);
    }

    private record SourceFetchResult(
        string SourceName,
        IReadOnlyList<RawCalendarEvent>? Events,
        CalendarError? Error);
}
