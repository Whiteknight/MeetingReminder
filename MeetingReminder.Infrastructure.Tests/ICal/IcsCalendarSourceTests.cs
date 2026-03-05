using AwesomeAssertions;
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
        events[0].Calendar.Should().Be("Test Calendar");
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
        error.CalendarSource.Should().Be("Test Calendar");
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
        source.Name.Should().Be("My Calendar");
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
