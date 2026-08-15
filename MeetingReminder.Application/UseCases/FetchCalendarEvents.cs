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

    public FetchCalendarEvents(IEnumerable<ICalendarSource> sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
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
    public async Task<Result<IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>>, Error>> Fetch(
        FetchCalendarEventsQuery query,
        CancellationToken cancellationToken)
    {
        var sourceList = _sources.ToList();

        if (sourceList.Count == 0)
            return CalendarError.NoSourcesConfigured();

        var fetchTasks = sourceList
            .Select(source => FetchFromSource(source, query, cancellationToken));

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

        return result.Match<SourceFetchResult>(
            events => new SourceFetchSuccess(source.Name, events),
            error => new SourceFetchFailure(source.Name, error));
    }

    private Result<IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>>, Error> AggregateAndEnrichResults(
        SourceFetchResult[] results)
    {
        // = new Dictionary<CalendarName, IReadOnlyList<MeetingEvent>>();
        var eventsByCalendar = results.OfType<SourceFetchSuccess>()
            .GroupBy(success => success.SourceName)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MeetingEvent>)group.SelectMany(success => success.Events.Select(EnrichRawEvent)).ToList());

        var errors = results.OfType<SourceFetchFailure>()
            .Select(f => f.Error)
            .ToList();

        // Succeed if at least one source responded (even with an empty list)
        // TODO: This case is probably always true, since it would only be 0 when there are no
        // configured sources but that case has already been filtered out.
        return eventsByCalendar.Count > 0
            ? eventsByCalendar
            : GetError(errors);
    }

    private static Result<IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>>, Error> GetError(List<CalendarError> errors)
        => errors switch
        {
            [] => CalendarError.NoEventsFound(),
            [CalendarError single] => single,
            var all => CalendarError.AllSourcesFailed(all)
        };

    private MeetingEvent EnrichRawEvent(RawCalendarEvent raw)
        => MeetingEvent.Create(
            id: new MeetingId(raw.Calendar, raw.Id),
            title: raw.Title,
            startTime: raw.StartTime,
            endTime: raw.EndTime,
            description: raw.Description,
            location: raw.Location,
            isAllDay: raw.IsAllDay,
            calendar: raw.Calendar,
            link: ExtractMeetingLink.Extract(new ExtractMeetingLinkQuery(raw.Description, raw.Location))
                .Match(l => (MeetingLink?)l, _ => null));

    private abstract record SourceFetchResult(CalendarName SourceName);
    private sealed record SourceFetchSuccess(CalendarName SourceName, IReadOnlyList<RawCalendarEvent> Events) : SourceFetchResult(SourceName);
    private sealed record SourceFetchFailure(CalendarName SourceName, CalendarError Error) : SourceFetchResult(SourceName);
}
