//using System.Threading.Channels;
//using AwesomeAssertions;
//using MeetingReminder.Application.UseCases;
//using MeetingReminder.Domain;
//using MeetingReminder.Domain.Calendars;
//using MeetingReminder.Domain.Configuration;
//using MeetingReminder.Domain.Meetings;
//using MeetingReminder.Infrastructure.Calendars;
//using NUnit.Framework;

//namespace MeetingReminder.Infrastructure.Tests.Calendars;

//[TestFixture]
//public class CalendarPollingServiceTests
//{
//    private Channel<CalendarEventsUpdated> _channel = null!;
//    private FakeTimeProvider _timeProvider = null!;
//    private DateTime _baseTime;

//    [SetUp]
//    public void SetUp()
//    {
//        _channel = Channel.CreateUnbounded<CalendarEventsUpdated>();
//        _baseTime = new DateTime(2026, 2, 19, 10, 0, 0, DateTimeKind.Utc);
//        _timeProvider = new FakeTimeProvider(_baseTime);
//    }

//    [TearDown]
//    public void TearDown()
//    {
//        _channel.Writer.Complete();
//    }

//    [Test]
//    public void Constructor_NullFetchCalendarEvents_ThrowsArgumentNullException()
//    {
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));

//        var action = () => new CalendarPollingService(
//            null!,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        action.Should().Throw<ArgumentNullException>()
//            .WithParameterName("fetchCalendarEvents");
//    }

//    [Test]
//    public void Constructor_NullChannel_ThrowsArgumentNullException()
//    {
//        var source = new FakeCalendarSource("test", []);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));

//        var action = () => new CalendarPollingService(
//            fetchCalendarEvents,
//            null!,
//            config,
//            _timeProvider);

//        action.Should().Throw<ArgumentNullException>()
//            .WithParameterName("calendarChannel");
//    }

//    [Test]
//    public void Constructor_PollingIntervalLessThanOneMinute_ThrowsArgumentException()
//    {
//        var source = new FakeCalendarSource("test", []);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromSeconds(30));

//        var action = () => new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        action.Should().Throw<ArgumentException>()
//            .WithParameterName("configuration");
//    }

//    [Test]
//    public void Constructor_NullConfiguration_UsesDefaultFiveMinuteInterval()
//    {
//        var source = new FakeCalendarSource("test", []);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);

//        // Should not throw - uses default 5 minute interval
//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            null!,
//            _timeProvider);

//        service.Should().NotBeNull();
//        service.Dispose();
//    }

//    [Test]
//    public void Constructor_CustomInterval_AcceptsValidInterval()
//    {
//        var source = new FakeCalendarSource("test", []);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(10));

//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        service.Should().NotBeNull();
//        service.Dispose();
//    }

//    [Test]
//    public void Constructor_OneMinuteInterval_AcceptsMinimumValidInterval()
//    {
//        var source = new FakeCalendarSource("test", []);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(1));

//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        service.Should().NotBeNull();
//        service.Dispose();
//    }

//    [Test]
//    public async Task StartAsync_SetsIsRunningToTrue()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.StartAsync();

//        service.IsRunning.Should().BeTrue();
//    }

//    [Test]
//    public async Task StopAsync_SetsIsRunningToFalse()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.StartAsync();
//        await service.StopAsync();

//        service.IsRunning.Should().BeFalse();
//    }

//    [Test]
//    public async Task StartAsync_CalledTwice_DoesNotThrow()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.StartAsync();
//        await service.StartAsync(); // Should not throw

//        service.IsRunning.Should().BeTrue();
//    }

//    [Test]
//    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.StopAsync(); // Should not throw

//        service.IsRunning.Should().BeFalse();
//    }

//    [Test]
//    public async Task StartAsync_ImmediatelyPollsOnStart()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.StartAsync();

//        // Wait a short time for the immediate poll to complete
//        await Task.Delay(100);

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update.Should().NotBeNull();
//        update!.AllEvents.Should().HaveCount(1);
//    }

//    [Test]
//    public async Task PollNowAsync_FetchesEventsAndWritesToChannel()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update.Should().NotBeNull();
//        update!.AllEvents.Should().HaveCount(1);
//        update.AllEvents[0].Id.Should().Be("1");
//    }

//    [Test]
//    public async Task PollNowAsync_FirstPoll_AllEventsAreAddedEvents()
//    {
//        var events = new List<RawCalendarEvent>
//        {
//            CreateRawEvent("1", "Meeting 1"),
//            CreateRawEvent("2", "Meeting 2")
//        };
//        var source = new FakeCalendarSource("test", events);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update!.AllEvents.Should().HaveCount(2);
//        update.AddedEvents.Should().HaveCount(2);
//        update.RemovedEventIds.Should().BeEmpty();
//    }

//    [Test]
//    public async Task PollNowAsync_NewEventAdded_DetectsAddedEvent()
//    {
//        var mutableSource = new MutableCalendarSource("test");
//        mutableSource.SetEvents([CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([mutableSource]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        // First poll
//        await service.PollNowAsync();
//        _channel.Reader.TryRead(out _);

//        // Add a new event
//        mutableSource.SetEvents([
//            CreateRawEvent("1", "Meeting 1"),
//            CreateRawEvent("2", "Meeting 2")
//        ]);

//        // Second poll
//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update!.AllEvents.Should().HaveCount(2);
//        update.AddedEvents.Should().HaveCount(1);
//        update.AddedEvents[0].Id.Should().Be("2");
//        update.RemovedEventIds.Should().BeEmpty();
//    }

//    [Test]
//    public async Task PollNowAsync_EventRemoved_DetectsRemovedEvent()
//    {
//        var mutableSource = new MutableCalendarSource("test");
//        mutableSource.SetEvents([
//            CreateRawEvent("1", "Meeting 1"),
//            CreateRawEvent("2", "Meeting 2")
//        ]);
//        var fetchCalendarEvents = new FetchCalendarEvents([mutableSource]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        // First poll
//        await service.PollNowAsync();
//        _channel.Reader.TryRead(out _);

//        // Remove an event
//        mutableSource.SetEvents([CreateRawEvent("1", "Meeting 1")]);

//        // Second poll
//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update!.AllEvents.Should().HaveCount(1);
//        update.AddedEvents.Should().BeEmpty();
//        update.RemovedEventIds.Should().HaveCount(1);
//        update.RemovedEventIds[0].Should().Be("2");
//    }

//    [Test]
//    public async Task PollNowAsync_EventAddedAndRemoved_DetectsBothChanges()
//    {
//        var mutableSource = new MutableCalendarSource("test");
//        mutableSource.SetEvents([
//            CreateRawEvent("1", "Meeting 1"),
//            CreateRawEvent("2", "Meeting 2")
//        ]);
//        var fetchCalendarEvents = new FetchCalendarEvents([mutableSource]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        // First poll
//        await service.PollNowAsync();
//        _channel.Reader.TryRead(out _);

//        // Remove event 2, add event 3
//        mutableSource.SetEvents([
//            CreateRawEvent("1", "Meeting 1"),
//            CreateRawEvent("3", "Meeting 3")
//        ]);

//        // Second poll
//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update!.AllEvents.Should().HaveCount(2);
//        update.AddedEvents.Should().HaveCount(1);
//        update.AddedEvents[0].Id.Should().Be("3");
//        update.RemovedEventIds.Should().HaveCount(1);
//        update.RemovedEventIds[0].Should().Be("2");
//    }

//    [Test]
//    public async Task PollNowAsync_NoChanges_ReportsEmptyAddedAndRemoved()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        // First poll
//        await service.PollNowAsync();
//        _channel.Reader.TryRead(out _);

//        // Second poll with same data
//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeTrue();
//        update!.AllEvents.Should().HaveCount(1);
//        update.AddedEvents.Should().BeEmpty();
//        update.RemovedEventIds.Should().BeEmpty();
//    }

//    [Test]
//    public async Task PollNowAsync_FetchFails_DoesNotWriteToChannel()
//    {
//        var source = new FakeCalendarSource("test", new CalendarError("Network error", "test"));
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        await service.PollNowAsync();

//        _channel.Reader.TryRead(out var update).Should().BeFalse();
//    }

//    [Test]
//    public async Task PollNowAsync_ConcurrentCalls_OnlyOneExecutes()
//    {
//        var slowSource = new SlowCalendarSource("test", 200, [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([slowSource]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        using var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        // Start two polls concurrently
//        var poll1 = service.PollNowAsync();
//        var poll2 = service.PollNowAsync();

//        await Task.WhenAll(poll1, poll2);

//        // Only one should have written to the channel
//        var count = 0;
//        while (_channel.Reader.TryRead(out _))
//            count++;

//        count.Should().Be(1);
//    }

//    [Test]
//    public void Dispose_CalledMultipleTimes_DoesNotThrow()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        service.Dispose();
//        service.Dispose(); // Should not throw
//    }

//    [Test]
//    public void PollNowAsync_AfterDispose_ThrowsObjectDisposedException()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        service.Dispose();

//        var action = async () => await service.PollNowAsync();

//        action.Should().ThrowAsync<ObjectDisposedException>();
//    }

//    [Test]
//    public void StartAsync_AfterDispose_ThrowsObjectDisposedException()
//    {
//        var source = new FakeCalendarSource("test", [CreateRawEvent("1", "Meeting 1")]);
//        var fetchCalendarEvents = new FetchCalendarEvents([source]);
//        var config = new FakeConfiguration(TimeSpan.FromMinutes(5));
//        var service = new CalendarPollingService(
//            fetchCalendarEvents,
//            _channel.Writer,
//            config,
//            _timeProvider);

//        service.Dispose();

//        var action = async () => await service.StartAsync();

//        action.Should().ThrowAsync<ObjectDisposedException>();
//    }

//    private RawCalendarEvent CreateRawEvent(string id, string title)
//    {
//        return new RawCalendarEvent(
//            Id: id,
//            Title: title,
//            StartTime: _baseTime.AddHours(1),
//            EndTime: _baseTime.AddHours(2),
//            Description: "Test description",
//            Location: "Test location",
//            IsAllDay: false,
//            CalendarSource: "test-calendar");
//    }

//    private class FakeTimeProvider : ITimeProvider
//    {
//        public FakeTimeProvider(DateTime utcNow)
//        {
//            UtcNow = utcNow;
//        }

//        public DateTime UtcNow { get; set; }
//        public DateTime Now => UtcNow.ToLocalTime();
//    }

//    private class FakeConfiguration : IAppConfiguration
//    {
//        public FakeConfiguration(TimeSpan pollingInterval)
//        {
//            PollingInterval = pollingInterval;
//        }

//        public TimeSpan PollingInterval { get; }
//        public IReadOnlyList<string> EnabledNotificationStrategies => [];
//        public INotificationThresholds Thresholds => new FakeThresholds();
//        public IReadOnlyList<ICalendarConfiguration> Calendars => [];
//    }

//    private class FakeThresholds : INotificationThresholds
//    {
//        public TimeSpan GentleMinutes => TimeSpan.FromMinutes(10);
//        public TimeSpan ModerateMinutes => TimeSpan.FromMinutes(5);
//        public TimeSpan UrgentMinutes => TimeSpan.FromMinutes(1);
//    }

//    private class FakeCalendarSource : ICalendarSource
//    {
//        private readonly IReadOnlyList<RawCalendarEvent>? _events;
//        private readonly CalendarError? _error;

//        public FakeCalendarSource(string sourceName, IReadOnlyList<RawCalendarEvent> events)
//        {
//            SourceName = sourceName;
//            _events = events;
//        }

//        public FakeCalendarSource(string sourceName, CalendarError error)
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

//    private class MutableCalendarSource : ICalendarSource
//    {
//        private IReadOnlyList<RawCalendarEvent> _events = [];

//        public MutableCalendarSource(string sourceName)
//        {
//            SourceName = sourceName;
//        }

//        public string SourceName { get; }

//        public void SetEvents(IReadOnlyList<RawCalendarEvent> events)
//        {
//            _events = events;
//        }

//        public Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
//            DateTime startTime,
//            DateTime endTime,
//            CancellationToken cancellationToken)
//        {
//            return Task.FromResult(Result.FromValue<IReadOnlyList<RawCalendarEvent>, CalendarError>(_events));
//        }
//    }

//    private class SlowCalendarSource : ICalendarSource
//    {
//        private readonly int _delayMs;
//        private readonly IReadOnlyList<RawCalendarEvent> _events;

//        public SlowCalendarSource(string sourceName, int delayMs, IReadOnlyList<RawCalendarEvent> events)
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
//}
