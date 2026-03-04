using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Infrastructure.ICal;

/// <summary>
/// Calendar source implementation that fetches and parses iCal/ICS data from a URL.
/// Returns raw calendar events without any enrichment (link extraction, etc.).
/// </summary>
public class IcsCalendarSource : ICalendarSource
{
    private readonly HttpClient _httpClient;
    private readonly string _sourceUrl;
    private readonly string _sourceName;

    /// <summary>
    /// Creates a new ICS calendar source.
    /// </summary>
    /// <param name="httpClient">HTTP client for fetching iCal data</param>
    /// <param name="sourceUrl">URL of the iCal/ICS feed</param>
    /// <param name="sourceName">Display name for this calendar source</param>
    public IcsCalendarSource(HttpClient httpClient, string sourceUrl, string sourceName)
    {
        _httpClient = NotNull(httpClient);
        _sourceUrl = NotNull(sourceUrl);
        _sourceName = NotNull(sourceName);
    }

    /// <inheritdoc />
    public string Name => _sourceName;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
        => await FetchICalData(cancellationToken)
            .BindAsync(data => ParseICalData(data, startTime, endTime));

    private async Task<Result<string, CalendarError>> FetchICalData(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(_sourceUrl, cancellationToken);
            return !response.IsSuccessStatusCode
                ? (Result<string, CalendarError>)CalendarError.HttpError(_sourceName, (int)response.StatusCode, response.ReasonPhrase)
                : await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return CalendarError.NetworkError(_sourceName, ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return CalendarError.RequestCancelled(_sourceName, ex);
        }
        catch (TaskCanceledException ex)
        {
            return CalendarError.RequestTimedOut(_sourceName, ex);
        }
    }

    private Result<IReadOnlyList<RawCalendarEvent>, CalendarError> ParseICalData(
        string icalData,
        DateTime startTime,
        DateTime endTime)
    {
        if (string.IsNullOrWhiteSpace(icalData))
            return CalendarError.EmptyData(_sourceName);

        try
        {
            // TODO: Need to filter the source calendar events before mapping
            // TODO: Need to expand reoccurring events
            var calendar = Calendar.Load(icalData);
            var events = new List<RawCalendarEvent>();
            if (calendar?.Events == null)
                return events.AsReadOnly();

            var allEvents = calendar.GetOccurrences(new CalDateTime(startTime)).TakeWhileBefore(new CalDateTime(endTime))
                .Select(MapToRawCalendarEvent)
                .Where(Result => !Result.IsError)
                .Select(Result => Result.GetValueOrDefault(null!))
                .OrderByDescending(e => e.StartTime)
                .ToList();

            foreach (var rawEvent in allEvents)
            {
                if (IsEventInTimeRange(rawEvent, startTime, endTime))
                    events.Add(rawEvent);
            }

            return events.AsReadOnly();
        }
        catch (Exception ex) when (IsParsingException(ex))
        {
            return CalendarError.ParseError(_sourceName, ex.Message, ex);
        }
    }

    private Result<RawCalendarEvent, CalendarError> MapToRawCalendarEvent(Occurrence occurrence)
    {
        try
        {
            var startTime = GetDateTimeOrDefault(occurrence.Period.StartTime, DateTime.UtcNow);
            var calendarEvent = occurrence.Source as CalendarEvent;
            if (calendarEvent == null)
                return CalendarError.MappingError(_sourceName, "Occurrence source is not a CalendarEvent");

            return new RawCalendarEvent(
                Id: calendarEvent.Uid ?? Guid.NewGuid().ToString(),
                Title: calendarEvent.Summary ?? "Untitled Event",
                StartTime: startTime,
                EndTime: GetDateTimeOrDefault(occurrence.Period.EffectiveEndTime ?? occurrence.Period.EndTime, startTime.AddHours(1)),
                Description: calendarEvent.Description ?? string.Empty,
                Location: calendarEvent.Location ?? string.Empty,
                IsAllDay: calendarEvent.IsAllDay,
                CalendarSource: _sourceName);
        }
        catch (Exception ex)
        {
            return CalendarError.MappingError(_sourceName, ex.Message, ex);
        }
    }

    private static DateTime GetDateTimeOrDefault(CalDateTime? icalDateTime, DateTime defaultValue)
        => icalDateTime == null
            ? defaultValue
            : icalDateTime.AsUtc;

    // Include events that start within the range or are ongoing during the range
    private static bool IsEventInTimeRange(RawCalendarEvent rawEvent, DateTime startTime, DateTime endTime)
        => rawEvent.StartTime < endTime
            && rawEvent.EndTime > startTime;

    private static bool IsParsingException(Exception ex)
        => ex is FormatException
            || ex is ArgumentException
            || ex is InvalidOperationException
            || ex is NullReferenceException
            || ex is System.Runtime.Serialization.SerializationException;
}
