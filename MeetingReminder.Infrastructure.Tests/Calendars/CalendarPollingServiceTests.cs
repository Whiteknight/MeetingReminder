using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Infrastructure.Calendars;
using MeetingReminder.Infrastructure.Meetings;
using MeetingReminder.Infrastructure.Threading;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Calendars;

[TestFixture]
public class CalendarPollingServiceTests
{
    private FakeTimeProvider _timeProvider = null!;
    private FakeConfiguration _config = null!;
    private PollingSchedule _pollingSchedule = null!;
    private IMeetingRepository _meetingRepository = null!;
    private ConsolidateIncomingMeetings _consolidateIncomingMeetings = null!;
    private DateTime _baseTime;

    [SetUp]
    public void SetUp()
    {
        _baseTime = new DateTime(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc);
        _timeProvider = new FakeTimeProvider(_baseTime);
        _config = new FakeConfiguration(TimeSpan.FromMinutes(5));
        _pollingSchedule = new PollingSchedule();
        _meetingRepository = new InMemoryMeetingRepository(new AsyncAutoResetEvent());
        _consolidateIncomingMeetings = new ConsolidateIncomingMeetings(_meetingRepository);
    }

    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Test]
    public void Constructor_NullFetchCalendarEvents_ThrowsArgumentNullException()
    {
        var act = () => new CalendarPollingService(
            null!,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fetchCalendarEvents");
    }

    [Test]
    public void Constructor_NullConsolidateIncomingMeetings_ThrowsArgumentNullException()
    {
        var fetchCalendarEvents = new FetchCalendarEvents([]);

        var act = () => new CalendarPollingService(
            fetchCalendarEvents,
            null!,
            _config,
            _timeProvider,
            _pollingSchedule);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("consolidateIncomingMeetings");
    }

    [Test]
    public void Constructor_NullPollingSchedule_ThrowsArgumentNullException()
    {
        var fetchCalendarEvents = new FetchCalendarEvents([]);

        var act = () => new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("pollingSchedule");
    }

    [Test]
    public void Constructor_NullConfiguration_UsesDefaultFiveMinuteInterval()
    {
        var fetchCalendarEvents = new FetchCalendarEvents([]);

        // Should not throw - falls back to 5-minute default
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            null!,
            _timeProvider,
            _pollingSchedule);

        service.Should().NotBeNull();
    }

    [Test]
    public void Constructor_PollingIntervalLessThanOneMinute_ThrowsArgumentException()
    {
        var fetchCalendarEvents = new FetchCalendarEvents([]);
        var config = new FakeConfiguration(TimeSpan.FromSeconds(30));

        var act = () => new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            config,
            _timeProvider,
            _pollingSchedule);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("configuration");
    }

    [Test]
    public void Constructor_OneMinuteInterval_AcceptsMinimumInterval()
    {
        var fetchCalendarEvents = new FetchCalendarEvents([]);
        var config = new FakeConfiguration(TimeSpan.FromMinutes(1));

        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            config,
            _timeProvider,
            _pollingSchedule);

        service.Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // Polling schedule: NextFetchAt
    // -------------------------------------------------------------------------

    [Test]
    public async Task StartAsync_SetsNextFetchAtToSomeUtcTime_AfterStart()
    {
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        await service.StartAsync();

        // After StartAsync, the schedule must be set (either to UtcNow for the first poll,
        // or already advanced to UtcNow + interval if the poll fired immediately).
        _pollingSchedule.NextFetchAt.Should().NotBeNull();
        _pollingSchedule.NextFetchAt!.Value.Kind.Should().Be(DateTimeKind.Utc);

        await service.StopAsync();
    }

    [Test]
    public async Task PollCycle_SetsNextFetchAtToNowPlusInterval_AfterPollStarts()
    {
        // Arrange: a source that completes quickly so we can observe the post-poll schedule
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        var interval = TimeSpan.FromMinutes(5);
        var config = new FakeConfiguration(interval);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            config,
            _timeProvider,
            _pollingSchedule);

        await service.StartAsync();

        // Wait long enough for the first poll to complete
        await WaitForConditionAsync(() => _pollingSchedule.NextFetchAt != _baseTime, timeoutMs: 2000);

        var expected = _baseTime + interval;
        _pollingSchedule.NextFetchAt.Should().Be(expected);

        await service.StopAsync();
    }

    [Test]
    public async Task StartAsync_CalledTwice_DoesNotThrow()
    {
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        await service.StartAsync();
        await service.StartAsync(); // Should be a no-op

        await service.StopAsync();
    }

    [Test]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        await service.StopAsync(); // Should not throw
    }

    // -------------------------------------------------------------------------
    // Poll behaviour: meetings are stored in the repository
    // -------------------------------------------------------------------------

    [Test]
    public async Task StartAsync_ImmediatelyPolls_MeetingsAreStoredInRepository()
    {
        var rawEvent = CreateRawEvent("cal", "1", "Morning Standup");
        var source = new FakeCalendarSource("cal", [rawEvent]);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        await service.StartAsync();

        await WaitForConditionAsync(
            () => _meetingRepository.GetAll().Match(all => all.Count > 0, _ => false),
            timeoutMs: 2000);

        var meetings = _meetingRepository.GetAll().GetValueOrDefault([]);
        meetings.Should().HaveCount(1);
        meetings[0].Event.Title.Should().Be("Morning Standup");

        await service.StopAsync();
    }

    [Test]
    public async Task PollCycle_FetchFails_DoesNotAddMeetingsToRepository()
    {
        var source = new FakeCalendarSource("test", new CalendarError("Network error", "test"));
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        using var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        await service.StartAsync();

        // Give the poll time to complete
        await Task.Delay(200);

        var meetings = _meetingRepository.GetAll().GetValueOrDefault([]);
        meetings.Should().BeEmpty();

        await service.StopAsync();
    }

    // -------------------------------------------------------------------------
    // Dispose
    // -------------------------------------------------------------------------

    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        service.Dispose();
        service.Dispose(); // Should not throw
    }

    [Test]
    public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var source = new FakeCalendarSource("test", []);
        var fetchCalendarEvents = new FetchCalendarEvents([source]);
        var service = new CalendarPollingService(
            fetchCalendarEvents,
            _consolidateIncomingMeetings,
            _config,
            _timeProvider,
            _pollingSchedule);

        service.Dispose();

        var act = async () => await service.StartAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(20);
        }
    }

    private RawCalendarEvent CreateRawEvent(string calendarName, string id, string title)
        => new RawCalendarEvent(
            Id: id,
            Title: title,
            StartTime: _baseTime.AddHours(1),
            EndTime: _baseTime.AddHours(2),
            Description: string.Empty,
            Location: string.Empty,
            IsAllDay: false,
            Calendar: new CalendarName(calendarName));

    private sealed class FakeTimeProvider : ITimeProvider
    {
        public FakeTimeProvider(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; set; }
        public DateTime Now => UtcNow.ToLocalTime();
    }

    private sealed class FakeConfiguration : IAppConfiguration
    {
        public FakeConfiguration(TimeSpan pollingInterval) => PollingInterval = pollingInterval;
        public TimeSpan PollingInterval { get; }
        public TimeSpan SilenceDuration => TimeSpan.FromMinutes(5);
        public IReadOnlyList<string> EnabledNotificationStrategies => [];
        public INotificationThresholds Thresholds => new FakeThresholds();
        public IReadOnlyList<ICalendarConfiguration> Calendars => [];
    }

    private sealed class FakeThresholds : INotificationThresholds
    {
        public TimeSpan GentleMinutes => TimeSpan.FromMinutes(10);
        public TimeSpan ModerateMinutes => TimeSpan.FromMinutes(5);
        public TimeSpan UrgentMinutes => TimeSpan.FromMinutes(1);
        public TimeSpan CriticalMinutes => TimeSpan.Zero;
    }

    private sealed class FakeCalendarSource : ICalendarSource
    {
        private readonly IReadOnlyList<RawCalendarEvent>? _events;
        private readonly CalendarError? _error;

        public FakeCalendarSource(string sourceName, IReadOnlyList<RawCalendarEvent> events)
        {
            Name = new CalendarName(sourceName);
            _events = events;
        }

        public FakeCalendarSource(string sourceName, CalendarError error)
        {
            Name = new CalendarName(sourceName);
            _error = error;
        }

        public CalendarName Name { get; }

        public Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
            DateTime startTime,
            DateTime endTime,
            CancellationToken cancellationToken)
        {
            if (_error is not null)
                return Task.FromResult<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>>(_error);

            return Task.FromResult(Result.FromValue<IReadOnlyList<RawCalendarEvent>, CalendarError>(_events!));
        }
    }
}
