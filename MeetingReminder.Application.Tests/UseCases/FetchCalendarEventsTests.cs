//using AwesomeAssertions;
//using MeetingReminder.Application.UseCases;
//using MeetingReminder.Domain;
//using MeetingReminder.Domain.Calendars;
//using MeetingReminder.Domain.Meetings;
//using NUnit.Framework;

//namespace MeetingReminder.Application.Tests.UseCases;

//[TestFixture]
//public class FetchCalendarEventsTests
//{
//    private DateTime _startTime;
//    private DateTime _endTime;

//    [SetUp]
//    public void SetUp()
//    {
//        _startTime = new DateTime(2026, 2, 19, 0, 0, 0);
//        _endTime = _startTime.AddDays(7);
//    }

//    private RawCalendarEvent CreateRawEvent(string id, string title) =>
//        new(
//            Id: id,
//            Title: title,
//            StartTime: _startTime.AddHours(1),
//            EndTime: _startTime.AddHours(2),
//            Description: "Test description",
//            Location: "Test location",
//            IsAllDay: false,
//            CalendarSource: "test-calendar");

//    private class TestCalendarSource : ICalendarSource
//    {
//        private readonly IReadOnlyList<RawCalendarEvent>? _events;
//        private readonly CalendarError? _error;

//        public TestCalendarSource(string sourceName, IReadOnlyList<RawCalendarEvent> events)
//        {
//            SourceName = sourceName;
//            _events = events;
//        }

//        public TestCalendarSource(string sourceName, CalendarError error)
//        {
//            SourceName = sourceName;
//            _error = error;
//        }

//        public string SourceName { get; }

//        public Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
//            DateTime startTime,
//            DateTime endTime,
//            CancellationToken cancellationToken)
//        {
//            if (_error is not null)
//                return Task.FromResult<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>>(_error);

//            return Task.FromResult(Result.FromValue<IReadOnlyList<RawCalendarEvent>, CalendarError>(_events!));
//        }
//    }

//    private class DelayedCalendarSource : ICalendarSource
//    {
//        private readonly int _delayMs;
//        private readonly IReadOnlyList<RawCalendarEvent> _events;

//        public DelayedCalendarSource(string sourceName, int delayMs, IReadOnlyList<RawCalendarEvent> events)
//        {
//            SourceName = sourceName;
//            _delayMs = delayMs;
//            _events = events;
//        }

//        public string SourceName { get; }

//        public async Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
//            DateTime startTime,
//            DateTime endTime,
//            CancellationToken cancellationToken)
//        {
//            await Task.Delay(_delayMs, cancellationToken);
//            return Result.FromValue<IReadOnlyList<RawCalendarEvent>, CalendarError>(_events);
//        }
//    }

//    [TestFixture]
//    public sealed class SingleSourceTests : FetchCalendarEventsTests
//    {
//        [Test]
//        public async Task ReturnsEvents_ReturnsSuccess()
//        {
//            var events = new List<RawCalendarEvent> { CreateRawEvent("1", "Meeting 1") };
//            var source = new TestCalendarSource("source1", events);
//            var fetchCalendarEvents = new FetchCalendarEvents([source]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsSuccess.Should().BeTrue();
//            var returnedEvents = result.Match(e => e, _ => []);
//            returnedEvents.Should().HaveCount(1);
//            returnedEvents[0].Id.Should().Be("1");
//        }

//        [Test]
//        public async Task Fails_ReturnsError()
//        {
//            var error = new CalendarError("Network error", "source1");
//            var source = new TestCalendarSource("source1", error);
//            var fetchCalendarEvents = new FetchCalendarEvents([source]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsError.Should().BeTrue();
//            result.Match(_ => null!, e => e).Message.Should().Contain("Network error");
//        }

//        [Test]
//        public async Task ReturnsEmptyList_ReturnsError()
//        {
//            var source = new TestCalendarSource("source1", new List<RawCalendarEvent>());
//            var fetchCalendarEvents = new FetchCalendarEvents([source]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsError.Should().BeTrue();
//            result.Match(_ => null!, e => e).Message.Should().Contain("No events found");
//        }
//    }

//    [TestFixture]
//    public sealed class MultipleSourceTests : FetchCalendarEventsTests
//    {
//        [Test]
//        public async Task AllSucceed_AggregatesEvents()
//        {
//            var events1 = new List<RawCalendarEvent> { CreateRawEvent("1", "Meeting 1") };
//            var events2 = new List<RawCalendarEvent> { CreateRawEvent("2", "Meeting 2") };
//            var source1 = new TestCalendarSource("source1", events1);
//            var source2 = new TestCalendarSource("source2", events2);
//            var fetchCalendarEvents = new FetchCalendarEvents([source1, source2]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsSuccess.Should().BeTrue();
//            var returnedEvents = result.Match(e => e, _ => []);
//            returnedEvents.Should().HaveCount(2);
//            returnedEvents.Select(e => e.Id).Should().Contain("1");
//            returnedEvents.Select(e => e.Id).Should().Contain("2");
//        }

//        [Test]
//        public async Task OneSucceedsOneFails_ReturnsSuccessWithPartialResults()
//        {
//            var events = new List<RawCalendarEvent> { CreateRawEvent("1", "Meeting 1") };
//            var source1 = new TestCalendarSource("source1", events);
//            var source2 = new TestCalendarSource("source2", new CalendarError("Failed", "source2"));
//            var fetchCalendarEvents = new FetchCalendarEvents([source1, source2]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsSuccess.Should().BeTrue();
//            var returnedEvents = result.Match(e => e, _ => []);
//            returnedEvents.Should().HaveCount(1);
//            returnedEvents[0].Id.Should().Be("1");
//        }

//        [Test]
//        public async Task AllFail_ReturnsAggregateError()
//        {
//            var source1 = new TestCalendarSource("source1", new CalendarError("Error 1", "source1"));
//            var source2 = new TestCalendarSource("source2", new CalendarError("Error 2", "source2"));
//            var fetchCalendarEvents = new FetchCalendarEvents([source1, source2]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsError.Should().BeTrue();
//            var error = result.Match(_ => null!, e => e);
//            error.Message.Should().Contain("2 calendar sources failed");
//            error.Message.Should().Contain("Error 1");
//            error.Message.Should().Contain("Error 2");
//        }
//    }

//    [TestFixture]
//    public sealed class EnrichmentTests : FetchCalendarEventsTests
//    {
//        [Test]
//        public async Task EventWithMeetingLink_ExtractsLink()
//        {
//            var events = new List<RawCalendarEvent>
//            {
//                new RawCalendarEvent(
//                    Id: "1",
//                    Title: "Meeting with link",
//                    StartTime: _startTime.AddHours(1),
//                    EndTime: _startTime.AddHours(2),
//                    Description: "Join at https://meet.google.com/abc-defg-hij",
//                    Location: "",
//                    IsAllDay: false,
//                    CalendarSource: "test")
//            };
//            var source = new TestCalendarSource("source1", events);
//            var fetchCalendarEvents = new FetchCalendarEvents([source]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsSuccess.Should().BeTrue();
//            var returnedEvents = result.Match(e => e, _ => []);
//            returnedEvents[0].Link.Should().NotBeNull();
//            returnedEvents[0].Link.Should().BeOfType<GoogleMeetLink>();
//        }

//        [Test]
//        public async Task EventWithoutMeetingLink_ReturnsNullLink()
//        {
//            var events = new List<RawCalendarEvent>
//            {
//                new RawCalendarEvent(
//                    Id: "1",
//                    Title: "Meeting without link",
//                    StartTime: _startTime.AddHours(1),
//                    EndTime: _startTime.AddHours(2),
//                    Description: "No link here",
//                    Location: "Room 101",
//                    IsAllDay: false,
//                    CalendarSource: "test")
//            };
//            var source = new TestCalendarSource("source1", events);
//            var fetchCalendarEvents = new FetchCalendarEvents([source]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsSuccess.Should().BeTrue();
//            var returnedEvents = result.Match(e => e, _ => []);
//            returnedEvents[0].Link.Should().BeNull();
//        }
//    }

//    [TestFixture]
//    public sealed class EdgeCaseTests : FetchCalendarEventsTests
//    {
//        [Test]
//        public async Task NoSources_ReturnsError()
//        {
//            var fetchCalendarEvents = new FetchCalendarEvents([]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);

//            result.IsError.Should().BeTrue();
//            result.Match(_ => null!, e => e).Message.Should().Contain("No calendar sources configured");
//        }

//        [Test]
//        public void Constructor_NullSources_ThrowsArgumentNullException()
//        {
//            var action = () => new FetchCalendarEvents(null!);

//            action.Should().Throw<ArgumentNullException>();
//        }

//        [Test]
//        public async Task FetchesConcurrently_CompletesInReasonableTime()
//        {
//            var source1 = new DelayedCalendarSource("source1", 100, [CreateRawEvent("1", "Meeting 1")]);
//            var source2 = new DelayedCalendarSource("source2", 100, [CreateRawEvent("2", "Meeting 2")]);
//            var source3 = new DelayedCalendarSource("source3", 100, [CreateRawEvent("3", "Meeting 3")]);
//            var fetchCalendarEvents = new FetchCalendarEvents([source1, source2, source3]);
//            var query = new FetchCalendarEventsQuery(_startTime, _endTime);

//            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
//            var result = await fetchCalendarEvents.Fetch(query, CancellationToken.None);
//            stopwatch.Stop();

//            result.IsSuccess.Should().BeTrue();
//            stopwatch.ElapsedMilliseconds.Should().BeLessThan(250);
//        }
//    }
//}
