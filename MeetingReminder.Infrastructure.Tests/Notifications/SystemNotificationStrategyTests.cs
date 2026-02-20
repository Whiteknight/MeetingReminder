using AwesomeAssertions;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Notifications;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Notifications;

[TestFixture]
public class SystemNotificationStrategyTests
{
    private ISystemNotificationProvider _mockProvider = null!;
    private SystemNotificationStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _mockProvider = Substitute.For<ISystemNotificationProvider>();
        _mockProvider.IsSupported.Returns(true);

        _strategy = new SystemNotificationStrategy(_mockProvider);
    }

    [Test]
    public void StrategyName_ReturnsSystemNotification()
    {
        _strategy.StrategyName.Should().Be("SystemNotification");
    }

    [Test]
    public void IsSupported_WhenProviderSupported_ReturnsTrue()
    {
        _mockProvider.IsSupported.Returns(true);

        _strategy.IsSupported.Should().BeTrue();
    }

    [Test]
    public void IsSupported_WhenProviderNotSupported_ReturnsFalse()
    {
        _mockProvider.IsSupported.Returns(false);

        _strategy.IsSupported.Should().BeFalse();
    }

    [Test]
    public async Task ExecuteOnCycleAsync_ReturnsSuccessWithoutNotifying()
    {
        // SystemNotificationStrategy doesn't execute on cycle, only on level change
        var meeting = CreateTestMeeting();

        var result = await _strategy.ExecuteOnCycleAsync(NotificationLevel.Gentle, meeting);

        result.IsSuccess.Should().BeTrue();
        await _mockProvider.DidNotReceive()
            .ShowNotificationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationLevel>());
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithNoneLevel_ReturnsSuccessWithoutNotifying()
    {
        var meeting = CreateTestMeeting();

        var result = await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.None, meeting);

        result.IsSuccess.Should().BeTrue();
        await _mockProvider.DidNotReceive()
            .ShowNotificationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationLevel>());
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WhenNotSupported_ReturnsError()
    {
        _mockProvider.IsSupported.Returns(false);
        var meeting = CreateTestMeeting();

        var result = await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meeting);

        result.IsError.Should().BeTrue();
        result.Match(
            _ => string.Empty,
            error => error.Message).Should().Contain("not supported");
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithGentleLevel_ShowsNotificationWithUpcomingPrefix()
    {
        var meeting = CreateTestMeeting();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meeting);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Is<string>(title => title.Contains("Upcoming") && title.Contains(meeting.Title)),
            Arg.Any<string>(),
            NotificationLevel.Gentle);
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithModerateLevel_ShowsNotificationWithSoonPrefix()
    {
        var meeting = CreateTestMeeting();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.Gentle, NotificationLevel.Moderate, meeting);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Is<string>(title => title.Contains("Soon") && title.Contains(meeting.Title)),
            Arg.Any<string>(),
            NotificationLevel.Moderate);
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithUrgentLevel_ShowsNotificationWithStartingSoonPrefix()
    {
        var meeting = CreateTestMeeting();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.Moderate, NotificationLevel.Urgent, meeting);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Is<string>(title => title.Contains("Starting Soon") && title.Contains(meeting.Title)),
            Arg.Any<string>(),
            NotificationLevel.Urgent);
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithCriticalLevel_ShowsNotificationWithNowPrefix()
    {
        var meeting = CreateTestMeeting();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.Urgent, NotificationLevel.Critical, meeting);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Is<string>(title => title.Contains("NOW") && title.Contains(meeting.Title)),
            Arg.Any<string>(),
            NotificationLevel.Critical);
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithMeetingLink_IncludesLinkInfoInBody()
    {
        var meetingWithLink = CreateTestMeetingWithLink();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meetingWithLink);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("link available")),
            Arg.Any<NotificationLevel>());
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WithLocation_IncludesLocationInBody()
    {
        var meeting = CreateTestMeeting();

        await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meeting);

        await _mockProvider.Received(1).ShowNotificationAsync(
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains(meeting.Location)),
            Arg.Any<NotificationLevel>());
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_WhenProviderThrows_ReturnsError()
    {
        _mockProvider.ShowNotificationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<NotificationLevel>())
            .Returns(Task.FromException(new InvalidOperationException("Notification service unavailable")));

        var meeting = CreateTestMeeting();

        var result = await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meeting);

        result.IsError.Should().BeTrue();
        result.Match(
            _ => string.Empty,
            error => error.Message).Should().Contain("Failed to show");
    }

    [Test]
    public async Task ExecuteOnLevelChangeAsync_ReturnsSuccess_WhenNotificationShown()
    {
        var meeting = CreateTestMeeting();

        var result = await _strategy.ExecuteOnLevelChangeAsync(
            NotificationLevel.None, NotificationLevel.Gentle, meeting);

        result.IsSuccess.Should().BeTrue();
    }

    private static MeetingEvent CreateTestMeeting() =>
        new(
            id: Guid.NewGuid().ToString(),
            title: "Test Meeting",
            startTime: DateTime.UtcNow.AddMinutes(5),
            endTime: DateTime.UtcNow.AddMinutes(65),
            description: "Test description",
            location: "Conference Room A",
            isAllDay: false,
            calendarSource: "test-calendar");

    private static MeetingEvent CreateTestMeetingWithLink() =>
        new(
            id: Guid.NewGuid().ToString(),
            title: "Test Meeting with Link",
            startTime: DateTime.UtcNow.AddMinutes(5),
            endTime: DateTime.UtcNow.AddMinutes(65),
            description: "Test description",
            location: "Virtual",
            isAllDay: false,
            calendarSource: "test-calendar",
            link: new GoogleMeetLink("https://meet.google.com/abc-defg-hij"));
}
