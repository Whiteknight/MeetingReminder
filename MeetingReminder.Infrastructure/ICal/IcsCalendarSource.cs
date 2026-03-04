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
        Name = NotNull(sourceName);
    }

    /// <inheritdoc />
    public string Name { get; }

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
                ? (Result<string, CalendarError>)CalendarError.HttpError(Name, (int)response.StatusCode, response.ReasonPhrase)
                : await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return CalendarError.NetworkError(Name, ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return CalendarError.RequestCancelled(Name, ex);
        }
        catch (TaskCanceledException ex)
        {
            return CalendarError.RequestTimedOut(Name, ex);
        }
    }

    private Result<IReadOnlyList<RawCalendarEvent>, CalendarError> ParseICalData(
        string icalData,
        DateTime startTime,
        DateTime endTime)
    {
        if (string.IsNullOrWhiteSpace(icalData))
            return CalendarError.EmptyData(Name);

        try
        {
            var calendar = Calendar.Load(icalData);
            if (calendar?.Events == null)
                return CalendarError.EmptyData(Name);

            return calendar.GetOccurrences(new CalDateTime(startTime)).TakeWhileBefore(new CalDateTime(endTime))
                .Select(MapToRawCalendarEvent)
                .Where(Result => !Result.IsError)
                .Select(Result => Result.GetValueOrDefault(null!))
                .OrderByDescending(e => e.StartTime)
                .Where(rawEvent => IsEventInTimeRange(rawEvent, startTime, endTime))
                .ToList()
                .AsReadOnly();
        }
        catch (Exception ex) when (IsParsingException(ex))
        {
            return CalendarError.ParseError(Name, ex.Message, ex);
        }
    }

    private Result<RawCalendarEvent, CalendarError> MapToRawCalendarEvent(Occurrence occurrence)
    {
        try
        {
            var startTime = GetDateTimeOrDefault(occurrence.Period.StartTime, DateTime.UtcNow);
            var calendarEvent = occurrence.Source as CalendarEvent;
            if (calendarEvent == null)
                return CalendarError.MappingError(Name, "Occurrence source is not a CalendarEvent");

            return new RawCalendarEvent(
                Id: calendarEvent.Uid ?? Guid.NewGuid().ToString(),
                Title: calendarEvent.Summary ?? "Untitled Event",
                StartTime: startTime,
                EndTime: GetDateTimeOrDefault(occurrence.Period.EffectiveEndTime ?? occurrence.Period.EndTime, startTime.AddHours(1)),
                Description: calendarEvent.Description ?? string.Empty,
                Location: calendarEvent.Location ?? string.Empty,
                IsAllDay: calendarEvent.IsAllDay,
                CalendarSource: Name);
        }
        catch (Exception ex)
        {
            return CalendarError.MappingError(Name, ex.Message, ex);
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
