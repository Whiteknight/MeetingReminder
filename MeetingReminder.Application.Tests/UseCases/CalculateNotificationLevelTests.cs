using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class CalculateNotificationLevelTests
{
    private CalculateNotificationLevel _calculateNotificationLevel = null!;
    private TestNotificationThresholds _defaultThresholds = null!;

    [SetUp]
    public void SetUp()
    {
        _calculateNotificationLevel = new CalculateNotificationLevel();
        _defaultThresholds = new TestNotificationThresholds(
            GentleMinutes: TimeSpan.FromMinutes(10),
            ModerateMinutes: TimeSpan.FromMinutes(5),
            UrgentMinutes: TimeSpan.FromMinutes(1));
    }

    private static MeetingEvent CreateMeeting(DateTime startTime) =>
        new(
            id: Guid.NewGuid().ToString(),
            title: "Test Meeting",
            startTime: startTime,
            endTime: startTime.AddHours(1),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendarSource: "test-calendar");

    private static MeetingEvent CreateAllDayMeeting(DateTime startTime) =>
        new(
            id: Guid.NewGuid().ToString(),
            title: "All Day Event",
            startTime: startTime.Date,
            endTime: startTime.Date.AddDays(1),
            description: "All day event description",
            location: string.Empty,
            isAllDay: true,
            calendarSource: "test-calendar");

    private record TestNotificationThresholds(
        TimeSpan GentleMinutes,
        TimeSpan ModerateMinutes,
        TimeSpan UrgentMinutes) : INotificationThresholds;

    private record TestCalendarNotificationRules(
        TimeSpan? NotificationWindowStart,
        TimeSpan? NotificationWindowEnd,
        int UrgencyMultiplier) : ICalendarNotificationRules
    {
        public bool IsWithinNotificationWindow(DateTime currentTime)
        {
            if (NotificationWindowStart is null || NotificationWindowEnd is null)
                return true;

            var timeOfDay = currentTime.TimeOfDay;

            if (NotificationWindowStart.Value <= NotificationWindowEnd.Value)
            {
                return timeOfDay >= NotificationWindowStart.Value
                    && timeOfDay <= NotificationWindowEnd.Value;
            }

            return timeOfDay >= NotificationWindowStart.Value
                || timeOfDay <= NotificationWindowEnd.Value;
        }
    }

    [TestFixture]
    public sealed class GentleThresholdTests : CalculateNotificationLevelTests
    {
        [Test]
        public void MeetingIn9Minutes_ReturnsGentle()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(9));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Gentle);
        }

        [Test]
        public void MeetingIn10Minutes_ReturnsGentle()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(10));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Gentle);
        }

        [Test]
        public void MeetingIn6Minutes_ReturnsGentle()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(6));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Gentle);
        }
    }

    [TestFixture]
    public sealed class ModerateThresholdTests : CalculateNotificationLevelTests
    {
        [Test]
        public void MeetingIn5Minutes_ReturnsModerate()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(5));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Moderate);
        }

        [Test]
        public void MeetingIn4Minutes_ReturnsModerate()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(4));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Moderate);
        }

        [Test]
        public void MeetingIn2Minutes_ReturnsModerate()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(2));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Moderate);
        }
    }

    [TestFixture]
    public sealed class UrgentThresholdTests : CalculateNotificationLevelTests
    {
        [Test]
        public void MeetingIn1Minute_ReturnsUrgent()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(1));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Urgent);
        }

        [Test]
        public void MeetingIn30Seconds_ReturnsUrgent()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddSeconds(30));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Urgent);
        }
    }

    [TestFixture]
    public sealed class CriticalThresholdTests : CalculateNotificationLevelTests
    {
        [Test]
        public void MeetingAtExactStartTime_ReturnsCritical()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime);
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Critical);
        }

        [Test]
        public void MeetingStarted1MinuteAgo_ReturnsCritical()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(-1));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Critical);
        }

        [Test]
        public void MeetingStarted10MinutesAgo_ReturnsCritical()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(-10));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Critical);
        }
    }

    [TestFixture]
    public sealed class NoNotificationTests : CalculateNotificationLevelTests
    {
        [Test]
        public void MeetingIn15Minutes_ReturnsNone()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(15));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.Critical).Should().Be(NotificationLevel.None);
        }

        [Test]
        public void MeetingIn1Hour_ReturnsNone()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddHours(1));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.Critical).Should().Be(NotificationLevel.None);
        }
    }

    [TestFixture]
    public sealed class AllDayEventTests : CalculateNotificationLevelTests
    {
        [Test]
        public void AllDayEvent_ReturnsNone()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateAllDayMeeting(currentTime);
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.Critical).Should().Be(NotificationLevel.None);
        }

        [Test]
        public void AllDayEventPastStartTime_ReturnsNone()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateAllDayMeeting(currentTime.AddHours(-2));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.Critical).Should().Be(NotificationLevel.None);
        }
    }

    [TestFixture]
    public sealed class NotificationTimeWindowTests : CalculateNotificationLevelTests
    {
        [Test]
        public void OutsideNotificationWindow_ReturnsNone()
        {
            var currentTime = new DateTime(2026, 2, 19, 7, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(1));
            var rules = new TestCalendarNotificationRules(
                NotificationWindowStart: TimeSpan.FromHours(9),
                NotificationWindowEnd: TimeSpan.FromHours(17),
                UrgencyMultiplier: 1);
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds, rules);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.Critical).Should().Be(NotificationLevel.None);
        }

        [Test]
        public void InsideNotificationWindow_ReturnsAppropriateLevel()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(1));
            var rules = new TestCalendarNotificationRules(
                NotificationWindowStart: TimeSpan.FromHours(9),
                NotificationWindowEnd: TimeSpan.FromHours(17),
                UrgencyMultiplier: 1);
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds, rules);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Urgent);
        }

        [Test]
        public void NullRules_TreatsAsAlwaysActive()
        {
            var currentTime = new DateTime(2026, 2, 19, 3, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(1));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, _defaultThresholds, null);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Urgent);
        }
    }

    [TestFixture]
    public sealed class CustomThresholdTests : CalculateNotificationLevelTests
    {
        [Test]
        public void CustomThresholds_UsesConfiguredValues()
        {
            var currentTime = new DateTime(2026, 2, 19, 10, 0, 0);
            var meeting = CreateMeeting(currentTime.AddMinutes(25));
            var customThresholds = new TestNotificationThresholds(
                GentleMinutes: TimeSpan.FromMinutes(30),
                ModerateMinutes: TimeSpan.FromMinutes(20),
                UrgentMinutes: TimeSpan.FromMinutes(10));
            var query = new CalculateNotificationLevelQuery(meeting, currentTime, customThresholds);

            var result = _calculateNotificationLevel.Calculate(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(level => level, _ => NotificationLevel.None).Should().Be(NotificationLevel.Gentle);
        }
    }
}
