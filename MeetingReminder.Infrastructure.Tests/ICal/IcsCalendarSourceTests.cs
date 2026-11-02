using AwesomeAssertions;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Infrastructure.ICal;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Net;

namespace MeetingReminder.Infrastructure.Tests.ICal;

[TestFixture]
public class IcsCalendarSourceTests
{
    private const string ValidICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:test-event-1@example.com
DTSTART:20260220T100000Z
DTEND:20260220T110000Z
SUMMARY:Test Meeting
DESCRIPTION:This is a test meeting with a link https://meet.google.com/abc-defg-hij
LOCATION:Conference Room A
END:VEVENT
END:VCALENDAR";

    private const string MultipleEventsICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:event-1@example.com
DTSTART:20260220T100000Z
DTEND:20260220T110000Z
SUMMARY:First Meeting
DESCRIPTION:First meeting description
END:VEVENT
BEGIN:VEVENT
UID:event-2@example.com
DTSTART:20260220T140000Z
DTEND:20260220T150000Z
SUMMARY:Second Meeting
DESCRIPTION:Second meeting description
END:VEVENT
END:VCALENDAR";

    private const string AllDayEventICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:all-day-event@example.com
DTSTART;VALUE=DATE:20260220
DTEND;VALUE=DATE:20260221
SUMMARY:All Day Event
DESCRIPTION:This is an all day event
END:VEVENT
END:VCALENDAR";

    // Two occurrences of the same recurring event on different days.
    // Both share the same UID - the distinguishing factor is the start time.
    private const string RecurringEventICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:recurring-event@example.com
DTSTART:20260220T100000Z
DTEND:20260220T110000Z
RRULE:FREQ=DAILY;COUNT=2
SUMMARY:Daily Standup
DESCRIPTION:Daily standup meeting
END:VEVENT
END:VCALENDAR";

    // A single event that has been rescheduled: the RECURRENCE-ID holds the original
    // time but DTSTART reflects the new time. We expect the instance ID to use DTSTART
    // so that the rescheduled occurrence gets a different ID from the original slot.
    private const string RescheduledEventICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:rescheduled-event@example.com
DTSTART:20260220T140000Z
DTEND:20260220T150000Z
SUMMARY:Rescheduled Meeting
DESCRIPTION:This meeting was moved
END:VEVENT
END:VCALENDAR";

    private const string MalformedICalData = @"This is not valid iCal data";

    private const string EmptyCalendarICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
END:VCALENDAR";

    [Test]
    public async Task FetchEvents_WithValidICalData_ReturnsRawEvents()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(ValidICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Title.Should().Be("Test Meeting");
        events[0].Description.Should().Contain("test meeting");
        events[0].Calendar.Should().Be(new CalendarName("Test Calendar"));
    }


    [Test]
    public async Task FetchEvents_WithMultipleEvents_ReturnsAllEventsInTimeRange()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(MultipleEventsICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(2);
        events.Should().Contain(e => e.Title == "First Meeting");
        events.Should().Contain(e => e.Title == "Second Meeting");
    }

    [Test]
    public async Task FetchEvents_WithAllDayEvent_SetsIsAllDayFlag()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(AllDayEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].IsAllDay.Should().BeTrue();
        events[0].Title.Should().Be("All Day Event");
    }

    [Test]
    public async Task FetchEvents_WithNetworkError_ReturnsCalendarError()
    {
        // Arrange
        var httpClient = CreateMockHttpClientWithException(new HttpRequestException("Network error"));
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        var error = result.Match(_ => null!, e => e);
        error.Message.Should().Contain("Network error");
        error.CalendarSource.Should().Be(new CalendarName("Test Calendar"));
    }

    [Test]
    public async Task FetchEvents_WithHttpErrorStatus_ReturnsCalendarError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("Not Found", HttpStatusCode.NotFound);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        var error = result.Match(_ => null!, e => e);
        error.Message.Should().Contain("404");
    }

    [Test]
    public async Task FetchEvents_WithMalformedICalData_ReturnsCalendarError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(MalformedICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        var error = result.Match(_ => null!, e => e);
        error.Message.Should().Contain("parse");
    }

    [Test]
    public async Task FetchEvents_WithEmptyCalendar_ReturnsEmptyList()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(EmptyCalendarICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().BeEmpty();
    }

    [Test]
    public async Task FetchEvents_WithEmptyContent_ReturnsCalendarError()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(string.Empty, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = DateTime.UtcNow;
        var endTime = DateTime.UtcNow.AddDays(7);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsError.Should().BeTrue();
        var error = result.Match(_ => null!, e => e);
        error.Message.Should().Contain("empty");
    }

    [Test]
    public async Task FetchEvents_ReturnsRawDataWithoutLinkExtraction()
    {
        // Arrange - Raw events should NOT have link extraction (that's done in enrichment)
        var httpClient = CreateMockHttpClient(ValidICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        // RawCalendarEvent doesn't have a Link property - it's just raw data
        events[0].Description.Should().Contain("meet.google.com"); // Link is in description, not extracted
    }

    [Test]
    public void SourceName_ReturnsConfiguredName()
    {
        // Arrange
        var httpClient = new HttpClient();
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "My Calendar");

        // Act & Assert
        source.Name.Should().Be(new CalendarName("My Calendar"));
    }

    [Test]
    public async Task FetchEvents_FiltersEventsOutsideTimeRange()
    {
        // Arrange
        var httpClient = CreateMockHttpClient(MultipleEventsICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        // Set time range that excludes the events (events are on 2026-02-20)
        var startTime = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().BeEmpty();
    }

    [Test]
    public async Task FetchEvents_EventIdIncludesUidAndStartTime()
    {
        // The ID must be UID_<starttime> so that rescheduled and recurring occurrences
        // each produce a unique ID rather than inheriting a stale acknowledgement state.
        var httpClient = CreateMockHttpClient(ValidICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Id.Should().Be("test-event-1@example.com_20260220T100000Z");
    }

    [Test]
    public async Task FetchEvents_RecurringEventOccurrencesHaveDistinctIds()
    {
        // Each daily occurrence shares a UID but must get a different ID because the
        // start times differ. Without this, acknowledging Monday's standup would cause
        // Tuesday's standup to start the day already acknowledged.
        var httpClient = CreateMockHttpClient(RecurringEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(2);

        var ids = events.Select(e => e.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().Contain("recurring-event@example.com_20260220T100000Z");
        ids.Should().Contain("recurring-event@example.com_20260221T100000Z");
    }

    [Test]
    public async Task FetchEvents_RescheduledEventIdReflectsNewStartTime()
    {
        // A rescheduled meeting has a different DTSTART from its original slot.
        // The instance ID must use DTSTART (the actual occurrence time) so that
        // acknowledging the original slot does not suppress alerts for the new time.
        var httpClient = CreateMockHttpClient(RescheduledEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Id.Should().Be("rescheduled-event@example.com_20260220T140000Z");
    }

    // An event at 17:00 Australia/Sydney (UTC+10), meaning its UTC time is 07:00.
    // When queried with a UTC-based "today" range, a parser that treats the range
    // as floating (no TZID) would misplace this event, either missing it entirely
    // or shifting it to the wrong day. With explicit "UTC" bounds the event must
    // be returned correctly.
    private const string TzidLateAfternoonEventICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:tzid-afternoon@example.com
DTSTART;TZID=Australia/Sydney:20260220T170000
DTEND;TZID=Australia/Sydney:20260220T180000
SUMMARY:Sydney Afternoon Meeting
DESCRIPTION:Meeting at 5 PM Sydney time
END:VEVENT
END:VCALENDAR";

    [Test]
    public async Task FetchEvents_TzidEventLateAfternoonLocalTime_IsReturnedWithinUtcDayRange()
    {
        // 17:00 Sydney (UTC+10) = 07:00 UTC on the same calendar date.
        // The UTC query window covers the full UTC day so the event must be included.
        var httpClient = CreateMockHttpClient(TzidLateAfternoonEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 20, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Title.Should().Be("Sydney Afternoon Meeting");
        // Confirm the stored time is UTC: 06:00 UTC (Sydney is UTC+11 in February due to daylight saving),
        // not 17:00 interpreted literally as UTC
        events[0].StartTime.Should().Be(new DateTime(2026, 2, 20, 6, 0, 0, DateTimeKind.Utc));
        events[0].StartTime.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public async Task FetchEvents_TzidEventOutsideUtcDayRange_IsExcluded()
    {
        // The event is at 07:00 UTC on 2026-02-20. A query for 2026-02-21 must not include it.
        var httpClient = CreateMockHttpClient(TzidLateAfternoonEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 2, 21, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().BeEmpty();
    }

    // A floating-time event (no TZID on DTSTART/DTEND). Per RFC 5545 floating times carry
    // no timezone. We treat them as system local time and convert to UTC on ingestion.
    // DTSTART:20260820T160000 means 16:00 in whatever timezone the local machine is in.
    private const string FloatingTimeEventICalData = @"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Test//Test//EN
BEGIN:VEVENT
UID:floating-event@example.com
DTSTART:20260820T160000
DTEND:20260820T170000
SUMMARY:Floating Time Meeting
DESCRIPTION:Event with no TZID
END:VEVENT
END:VCALENDAR";

    [Test]
    public async Task FetchEvents_FloatingTimeEvent_IsInterpretedAsLocalTime()
    {
        // A floating DTSTART of 16:00 must be converted from local time to UTC.
        // The stored StartTime must therefore NOT equal 16:00 UTC for any machine
        // that is not on UTC, and its Kind must be Utc.
        var httpClient = CreateMockHttpClient(FloatingTimeEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 8, 20, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Title.Should().Be("Floating Time Meeting");
        events[0].StartTime.Kind.Should().Be(DateTimeKind.Utc);

        // The stored UTC time must equal local 16:00 converted to UTC.
        var expectedUtc = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 8, 20, 16, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.Local);
        events[0].StartTime.Should().Be(expectedUtc);
    }

    [Test]
    public async Task FetchEvents_FloatingTimeEvent_InstanceIdEndsWithLiteralZ()
    {
        // The Z suffix in the instance ID is always a literal UTC marker.
        // It must be present regardless of whether the source event had a TZID.
        var httpClient = CreateMockHttpClient(FloatingTimeEventICalData, HttpStatusCode.OK);
        var source = new IcsCalendarSource(httpClient, "https://example.com/calendar.ics", "Test Calendar");
        var startTime = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2026, 8, 20, 23, 59, 59, DateTimeKind.Utc);

        var result = await source.FetchEvents(startTime, endTime, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var events = result.Match(e => e, _ => null!);
        events.Should().HaveCount(1);
        events[0].Id.Should().StartWith("floating-event@example.com_");
        events[0].Id.Should().EndWith("Z");
        // The timestamp portion must be the UTC digits, not the local digits
        var expectedTimestamp = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2026, 8, 20, 16, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.Local)
            .ToString("yyyyMMddTHHmmss");
        events[0].Id.Should().Be($"floating-event@example.com_{expectedTimestamp}Z");
    }

    private static HttpClient CreateMockHttpClient(string responseContent, HttpStatusCode statusCode)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        return new HttpClient(mockHandler.Object);
    }

    private static HttpClient CreateMockHttpClientWithException(Exception exception)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        return new HttpClient(mockHandler.Object);
    }
}
