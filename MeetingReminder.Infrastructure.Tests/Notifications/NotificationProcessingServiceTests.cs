using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Notifications;
using NUnit.Framework;
using System.Threading.Channels;

namespace MeetingReminder.Infrastructure.Tests.Notifications;

[TestFixture]
public class NotificationProcessingServiceTests
{
    private Channel<CalendarEventsUpdated> _calendarChannel = null!;
    private Channel<NotificationStateChanged> _notificationChannel = null!;
    private FakeTimeProvider _timeProvider = null!;
    private DateTime _baseTime;
    private CalculateNotificationLevel _calculateNotificationLevel = null!;

    [SetUp]
    public void SetUp()
    {
        _calendarChannel = Channel.CreateUnbounded<CalendarEventsUpdated>();
        _notificationChannel = Channel.CreateUnbounded<NotificationStateChanged>();
        _baseTime = new DateTime(2026, 2, 19, 10, 0, 0, DateTimeKind.Utc);
        _timeProvider = new FakeTimeProvider(_baseTime);
        _calculateNotificationLevel = new CalculateNotificationLevel();
    }

    [TearDown]
    public void TearDown()
    {
        _calendarChannel.Writer.Complete();
        _notificationChannel.Writer.Complete();
    }


    [Test]
    public void Constructor_NullCalendarChannel_ThrowsArgumentNullException()
    {
        var config = CreateDefaultConfig();

        var action = () => new NotificationProcessingService(
            null!,
            _notificationChannel.Writer,
            [],
            _calculateNotificationLevel,
            config,
            _timeProvider);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("calendarChannel");
    }

    [Test]
    public void Constructor_NullNotificationChannel_ThrowsArgumentNullException()
    {
        var config = CreateDefaultConfig();

        var action = () => new NotificationProcessingService(
            _calendarChannel.Reader,
            null!,
            [],
            _calculateNotificationLevel,
            config,
            _timeProvider);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationChannel");
    }

    [Test]
    public void Constructor_NullCalculateNotificationLevel_ThrowsArgumentNullException()
    {
        var config = CreateDefaultConfig();

        var action = () => new NotificationProcessingService(
            _calendarChannel.Reader,
            _notificationChannel.Writer,
            [],
            null!,
            config,
            _timeProvider);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("calculateNotificationLevel");
    }

    [Test]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        var action = () => new NotificationProcessingService(
            _calendarChannel.Reader,
            _notificationChannel.Writer,
            [],
            _calculateNotificationLevel,
            null!,
            _timeProvider);

        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }

    [Test]
    public void Constructor_ValidParameters_CreatesService()
    {
        var config = CreateDefaultConfig();

        using var service = new NotificationProcessingService(
            _calendarChannel.Reader,
            _notificationChannel.Writer,
            [],
            _calculateNotificationLevel,
            config,
            _timeProvider);

        service.Should().NotBeNull();
    }

    [Test]
    public async Task StartAsync_SetsIsRunningToTrue()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        await service.StartAsync();

        service.IsRunning.Should().BeTrue();
    }

    [Test]
    public async Task StopAsync_SetsIsRunningToFalse()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        await service.StartAsync();
        await service.StopAsync();

        service.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task StartAsync_CalledTwice_DoesNotThrow()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        await service.StartAsync();
        await service.StartAsync(); // Should not throw

        service.IsRunning.Should().BeTrue();
    }

    [Test]
    public async Task StopAsync_WhenNotStarted_DoesNotThrow()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        await service.StopAsync(); // Should not throw

        service.IsRunning.Should().BeFalse();
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithNoMeetings_DoesNotWriteToChannel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithMeetingInGentleThreshold_WritesToChannel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting starts in 8 minutes (within gentle threshold of 10 minutes)
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(8));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out var notification).Should().BeTrue();
        notification.Should().NotBeNull();
        notification!.ActiveNotifications.Should().HaveCount(1);
        notification.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Gentle);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithMeetingInModerateThreshold_SetsModerateLevel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting starts in 3 minutes (within moderate threshold of 5 minutes)
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(3));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out var notification).Should().BeTrue();
        notification!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Moderate);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithMeetingInUrgentThreshold_SetsUrgentLevel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting starts in 30 seconds (within urgent threshold of 1 minute)
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddSeconds(30));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out var notification).Should().BeTrue();
        notification!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Urgent);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithMeetingAtStartTime_SetsCriticalLevel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting starts now
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime);
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out var notification).Should().BeTrue();
        notification!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Critical);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithMeetingPastStartTime_SetsCriticalLevel()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting started 5 minutes ago
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(-5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out var notification).Should().BeTrue();
        notification!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Critical);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithAllDayEvent_DoesNotNotify()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // All-day event
        var meeting = CreateAllDayMeeting("1", "All Day Event", _baseTime);
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task AcknowledgeMeeting_ExistingMeeting_ReturnsTrue()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        var result = service.AcknowledgeMeeting("1");

        result.Should().BeTrue();
    }

    [Test]
    public async Task AcknowledgeMeeting_NonExistentMeeting_ReturnsFalse()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        var result = service.AcknowledgeMeeting("non-existent");

        result.Should().BeFalse();
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_AcknowledgedMeeting_DoesNotNotify()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        // Acknowledge the meeting
        service.AcknowledgeMeeting("1");

        await service.ProcessNotificationsNowAsync();

        _notificationChannel.Reader.TryRead(out _).Should().BeFalse();
    }

    [Test]
    public async Task CalendarUpdate_RemovedMeeting_RemovesFromState()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Add a meeting
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        service.MeetingStates.Should().ContainKey("1");

        // Remove the meeting
        var removeUpdate = new CalendarEventsUpdated(
            AllEvents: [],
            AddedEvents: [],
            RemovedEventIds: ["1"],
            OccurredAt: _baseTime);
        await _calendarChannel.Writer.WriteAsync(removeUpdate);
        await Task.Delay(100); // Allow time for channel reader to process

        service.MeetingStates.Should().NotContainKey("1");
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithEnabledStrategy_ExecutesStrategy()
    {
        var strategy = new FakeNotificationStrategy("TestStrategy", isSupported: true);
        var config = CreateConfigWithStrategies(["TestStrategy"]);
        using var service = CreateService(config, [strategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        // Both cycle and level change methods are called on first notification
        strategy.CycleExecutionCount.Should().Be(1);
        strategy.LevelChangeExecutionCount.Should().Be(1);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithDisabledStrategy_DoesNotExecuteStrategy()
    {
        var strategy = new FakeNotificationStrategy("TestStrategy", isSupported: true);
        var config = CreateConfigWithStrategies([]); // No strategies enabled
        using var service = CreateService(config, [strategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        strategy.ExecutionCount.Should().Be(0);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_WithUnsupportedStrategy_DoesNotExecuteStrategy()
    {
        var strategy = new FakeNotificationStrategy("TestStrategy", isSupported: false);
        var config = CreateConfigWithStrategies(["TestStrategy"]);
        using var service = CreateService(config, [strategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        strategy.ExecutionCount.Should().Be(0);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_StrategyFails_ContinuesWithOtherStrategies()
    {
        var failingStrategy = new FakeNotificationStrategy("FailingStrategy", isSupported: true, shouldFail: true);
        var successStrategy = new FakeNotificationStrategy("SuccessStrategy", isSupported: true);
        var config = CreateConfigWithStrategies(["FailingStrategy", "SuccessStrategy"]);
        using var service = CreateService(config, [failingStrategy, successStrategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        // Both strategies should have been attempted (cycle + level change for each)
        failingStrategy.CycleExecutionCount.Should().Be(1);
        successStrategy.CycleExecutionCount.Should().Be(1);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_StrategyThrowsException_ContinuesWithOtherStrategies()
    {
        var throwingStrategy = new FakeNotificationStrategy("ThrowingStrategy", isSupported: true, shouldThrow: true);
        var successStrategy = new FakeNotificationStrategy("SuccessStrategy", isSupported: true);
        var config = CreateConfigWithStrategies(["ThrowingStrategy", "SuccessStrategy"]);
        using var service = CreateService(config, [throwingStrategy, successStrategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        await service.ProcessNotificationsNowAsync();

        // Both strategies should have been attempted (cycle + level change for each)
        throwingStrategy.CycleExecutionCount.Should().Be(1);
        successStrategy.CycleExecutionCount.Should().Be(1);
    }

    [Test]
    public async Task ProcessNotificationsNowAsync_SameLevelOnSubsequentCycle_OnlyExecutesCycleMethod()
    {
        var strategy = new FakeNotificationStrategy("TestStrategy", isSupported: true);
        var config = CreateConfigWithStrategies(["TestStrategy"]);
        using var service = CreateService(config, [strategy]);

        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddMinutes(5));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        // First notification - both cycle and level change
        await service.ProcessNotificationsNowAsync();
        strategy.CycleExecutionCount.Should().Be(1);
        strategy.LevelChangeExecutionCount.Should().Be(1);

        // Second notification at same level - only cycle
        await service.ProcessNotificationsNowAsync();
        strategy.CycleExecutionCount.Should().Be(2);
        strategy.LevelChangeExecutionCount.Should().Be(1); // Still 1, no level change
    }



    [Test]
    public async Task ProcessNotificationsNowAsync_NotificationLevelOnlyEscalates()
    {
        var config = CreateDefaultConfig();
        using var service = CreateService(config);

        // Meeting starts in 30 seconds (urgent)
        var meeting = CreateMeeting("1", "Test Meeting", _baseTime.AddSeconds(30));
        await StartServiceAndWriteCalendarUpdate(service, meeting);

        // First notification - should be urgent
        await service.ProcessNotificationsNowAsync();
        _notificationChannel.Reader.TryRead(out var notification1).Should().BeTrue();
        notification1!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Urgent);

        // Advance time so meeting is now in gentle threshold (8 minutes away)
        // But level should NOT decrease
        _timeProvider.UtcNow = _baseTime.AddMinutes(-7).AddSeconds(-30);

        await service.ProcessNotificationsNowAsync();
        _notificationChannel.Reader.TryRead(out var notification2).Should().BeTrue();
        // Level should still be Urgent (not decreased to Gentle)
        notification2!.ActiveNotifications[0].CurrentLevel.Should().Be(NotificationLevel.Urgent);
    }



    [Test]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var config = CreateDefaultConfig();
        var service = CreateService(config);

        service.Dispose();
        service.Dispose(); // Should not throw
    }

    [Test]
    public void ProcessNotificationsNowAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var config = CreateDefaultConfig();
        var service = CreateService(config);

        service.Dispose();

        var action = async () => await service.ProcessNotificationsNowAsync();

        action.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public void StartAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var config = CreateDefaultConfig();
        var service = CreateService(config);

        service.Dispose();

        var action = async () => await service.StartAsync();

        action.Should().ThrowAsync<ObjectDisposedException>();
    }



    private NotificationProcessingService CreateService(
        IAppConfiguration config,
        IEnumerable<INotificationStrategy>? strategies = null)
    {
        return new NotificationProcessingService(
            _calendarChannel.Reader,
            _notificationChannel.Writer,
            strategies ?? [],
            _calculateNotificationLevel,
            config,
            _timeProvider);
    }

    private static FakeConfiguration CreateDefaultConfig()
    {
        return new FakeConfiguration(
            pollingInterval: TimeSpan.FromMinutes(5),
            enabledStrategies: []);
    }

    private static FakeConfiguration CreateConfigWithStrategies(IReadOnlyList<string> strategies)
    {
        return new FakeConfiguration(
            pollingInterval: TimeSpan.FromMinutes(5),
            enabledStrategies: strategies);
    }

    private MeetingEvent CreateMeeting(string id, string title, DateTime startTime)
    {
        return new MeetingEvent(
            id: id,
            title: title,
            startTime: startTime,
            endTime: startTime.AddHours(1),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendarSource: "test-calendar");
    }

    private MeetingEvent CreateAllDayMeeting(string id, string title, DateTime date)
    {
        return new MeetingEvent(
            id: id,
            title: title,
            startTime: date.Date,
            endTime: date.Date.AddDays(1),
            description: "All day event",
            location: "",
            isAllDay: true,
            calendarSource: "test-calendar");
    }

    private async Task StartServiceAndWriteCalendarUpdate(
        NotificationProcessingService service,
        params MeetingEvent[] meetings)
    {
        await service.StartAsync();

        var update = new CalendarEventsUpdated(
            AllEvents: meetings.ToList(),
            AddedEvents: meetings.ToList(),
            RemovedEventIds: [],
            OccurredAt: _baseTime);
        await _calendarChannel.Writer.WriteAsync(update);

        // Allow time for channel reader to process
        await Task.Delay(100);
    }

    private async Task WriteCalendarUpdate(params MeetingEvent[] meetings)
    {
        var update = new CalendarEventsUpdated(
            AllEvents: meetings.ToList(),
            AddedEvents: meetings.ToList(),
            RemovedEventIds: [],
            OccurredAt: _baseTime);
        await _calendarChannel.Writer.WriteAsync(update);
    }



    private class FakeTimeProvider : ITimeProvider
    {
        public FakeTimeProvider(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
        public DateTime Now => UtcNow.ToLocalTime();
    }

    private class FakeConfiguration : IAppConfiguration
    {
        public FakeConfiguration(TimeSpan pollingInterval, IReadOnlyList<string> enabledStrategies)
        {
            PollingInterval = pollingInterval;
            EnabledNotificationStrategies = enabledStrategies;
        }

        public TimeSpan PollingInterval { get; }
        public IReadOnlyList<string> EnabledNotificationStrategies { get; }
        public INotificationThresholds Thresholds => new FakeThresholds();
        public IReadOnlyList<ICalendarConfiguration> Calendars => [];
    }

    private class FakeThresholds : INotificationThresholds
    {
        public TimeSpan GentleMinutes => TimeSpan.FromMinutes(10);
        public TimeSpan ModerateMinutes => TimeSpan.FromMinutes(5);
        public TimeSpan UrgentMinutes => TimeSpan.FromMinutes(1);
    }

    private class FakeNotificationStrategy : INotificationStrategy
    {
        private readonly bool _shouldFail;
        private readonly bool _shouldThrow;

        public FakeNotificationStrategy(
            string strategyName,
            bool isSupported,
            bool shouldFail = false,
            bool shouldThrow = false)
        {
            StrategyName = strategyName;
            IsSupported = isSupported;
            _shouldFail = shouldFail;
            _shouldThrow = shouldThrow;
        }

        public string StrategyName { get; }
        public bool IsSupported { get; }
        public int CycleExecutionCount { get; private set; }
        public int LevelChangeExecutionCount { get; private set; }
        public int ExecutionCount => CycleExecutionCount + LevelChangeExecutionCount;
        public NotificationLevel? LastLevel { get; private set; }
        public NotificationLevel? LastPreviousLevel { get; private set; }
        public MeetingEvent? LastMeeting { get; private set; }

        public Task<Result<Unit, NotificationError>> ExecuteOnCycleAsync(NotificationLevel level, MeetingEvent meeting)
        {
            CycleExecutionCount++;
            LastLevel = level;
            LastMeeting = meeting;

            if (_shouldThrow)
                throw new InvalidOperationException("Test exception");

            if (_shouldFail)
                return Task.FromResult<Result<Unit, NotificationError>>(
                    new NotificationError("Test failure", StrategyName));

            return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
        }

        public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(
            NotificationLevel previousLevel,
            NotificationLevel newLevel,
            MeetingEvent meeting)
        {
            LevelChangeExecutionCount++;
            LastPreviousLevel = previousLevel;
            LastLevel = newLevel;
            LastMeeting = meeting;

            if (_shouldThrow)
                throw new InvalidOperationException("Test exception");

            if (_shouldFail)
                return Task.FromResult<Result<Unit, NotificationError>>(
                    new NotificationError("Test failure", StrategyName));

            return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
        }
    }
}
