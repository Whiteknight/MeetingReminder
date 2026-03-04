using AwesomeAssertions;
using MeetingReminder.Domain.Meetings;
using NUnit.Framework;

namespace MeetingReminder.Domain.Tests.Meetings;

/// <summary>
/// Unit tests for MeetingEvent domain entity.
/// Tests domain logic methods and constructor validation.
/// </summary>
[TestFixture]
public class MeetingEventTests
{
    private static MeetingEvent CreateTestMeeting(
        DateTime? startTime = null,
        DateTime? endTime = null,
        bool isAllDay = false)
    {
        var start = startTime ?? DateTime.Now.AddHours(1);
        var end = endTime ?? start.AddHours(1);

        return MeetingEvent.Create(
            id: new MeetingId("TestCalendar", "test-id"),
            title: "Test Meeting",
            startTime: start,
            endTime: end,
            description: "Test description",
            location: "Test location",
            isAllDay: isAllDay,
            calendarSource: "TestCalendar");
    }

    [TestFixture]
    public sealed class ConstructorValidation : MeetingEventTests
    {
        [Test]
        public void WhenEndTimeBeforeStartTime_ThrowsArgumentException()
        {
            var startTime = DateTime.Now.AddHours(2);
            var endTime = DateTime.Now.AddHours(1);

            var act = () => MeetingEvent.Create(
                id: new MeetingId("TestCalendar", "test-id"),
                title: "Test Meeting",
                startTime: startTime,
                endTime: endTime,
                description: "Test",
                location: "Test",
                isAllDay: false,
                calendarSource: "TestCalendar");

            act.Should().Throw<ArgumentException>()
                .WithMessage("*End time must be after start time*");
        }

        [Test]
        public void WhenIdIsEmpty_ThrowsArgumentException()
        {
            var act = () => MeetingEvent.Create(
                id: new MeetingId("TestCalendar", ""),
                title: "Test Meeting",
                startTime: DateTime.Now,
                endTime: DateTime.Now.AddHours(1),
                description: "Test",
                location: "Test",
                isAllDay: false,
                calendarSource: "TestCalendar");

            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void WhenTitleIsNull_ThrowsArgumentNullException()
        {
            var act = () => MeetingEvent.Create(
                id: new MeetingId("TestCalendar", "test-id"),
                title: null!,
                startTime: DateTime.Now,
                endTime: DateTime.Now.AddHours(1),
                description: "Test",
                location: "Test",
                isAllDay: false,
                calendarSource: "TestCalendar");

            act.Should().Throw<ArgumentNullException>();
        }
    }

    [TestFixture]
    public sealed class GetTimeUntilStartTests : MeetingEventTests
    {
        [Test]
        public void WhenMeetingInFuture_ReturnsPositiveTimeSpan()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var currentTime = new DateTime(2025, 1, 15, 13, 0, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: startTime.AddHours(1));

            var result = meeting.GetTimeUntilStart(currentTime);

            result.Should().Be(TimeSpan.FromHours(1));
        }

        [Test]
        public void WhenMeetingStarted_ReturnsNegativeTimeSpan()
        {
            var startTime = new DateTime(2025, 1, 15, 13, 0, 0);
            var currentTime = new DateTime(2025, 1, 15, 13, 30, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: startTime.AddHours(1));

            var result = meeting.GetTimeUntilStart(currentTime);

            result.Should().Be(TimeSpan.FromMinutes(-30));
        }

        [Test]
        public void WhenExactlyAtStartTime_ReturnsZero()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: startTime.AddHours(1));

            var result = meeting.GetTimeUntilStart(startTime);

            result.Should().Be(TimeSpan.Zero);
        }
    }

    [TestFixture]
    public sealed class IsInProgressTests : MeetingEventTests
    {
        [Test]
        public void WhenCurrentTimeBeforeStart_ReturnsFalse()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var endTime = new DateTime(2025, 1, 15, 15, 0, 0);
            var currentTime = new DateTime(2025, 1, 15, 13, 30, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: endTime);

            var result = meeting.IsInProgress(currentTime);

            result.Should().BeFalse();
        }

        [Test]
        public void WhenCurrentTimeDuringMeeting_ReturnsTrue()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var endTime = new DateTime(2025, 1, 15, 15, 0, 0);
            var currentTime = new DateTime(2025, 1, 15, 14, 30, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: endTime);

            var result = meeting.IsInProgress(currentTime);

            result.Should().BeTrue();
        }

        [Test]
        public void WhenExactlyAtStartTime_ReturnsTrue()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var endTime = new DateTime(2025, 1, 15, 15, 0, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: endTime);

            var result = meeting.IsInProgress(startTime);

            result.Should().BeTrue();
        }

        [Test]
        public void WhenExactlyAtEndTime_ReturnsFalse()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var endTime = new DateTime(2025, 1, 15, 15, 0, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: endTime);

            var result = meeting.IsInProgress(endTime);

            result.Should().BeFalse();
        }

        [Test]
        public void WhenCurrentTimeAfterEnd_ReturnsFalse()
        {
            var startTime = new DateTime(2025, 1, 15, 14, 0, 0);
            var endTime = new DateTime(2025, 1, 15, 15, 0, 0);
            var currentTime = new DateTime(2025, 1, 15, 16, 0, 0);
            var meeting = CreateTestMeeting(startTime: startTime, endTime: endTime);

            var result = meeting.IsInProgress(currentTime);

            result.Should().BeFalse();
        }
    }
}
