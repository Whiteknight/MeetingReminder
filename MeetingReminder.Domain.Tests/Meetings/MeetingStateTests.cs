using AwesomeAssertions;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using NUnit.Framework;

namespace MeetingReminder.Domain.Tests.Meetings;

/// <summary>
/// Unit tests for MeetingState domain entity.
/// Tests notification level escalation and acknowledgement logic.
/// </summary>
[TestFixture]
public class MeetingStateTests
{
    private static MeetingEvent CreateTestMeeting()
    {
        return new MeetingEvent(
            id: "test-id",
            title: "Test Meeting",
            startTime: DateTime.Now.AddHours(1),
            endTime: DateTime.Now.AddHours(2),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendarSource: "TestCalendar");
    }

    [TestFixture]
    public sealed class UpdateNotificationLevelTests : MeetingStateTests
    {
        [Test]
        public void WhenEscalating_UpdatesLevel()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.CurrentLevel.Should().Be(NotificationLevel.None);

            state.UpdateNotificationLevel(NotificationLevel.Gentle);

            state.CurrentLevel.Should().Be(NotificationLevel.Gentle);
        }

        [Test]
        public void WhenDeescalating_DoesNotUpdateLevel()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.UpdateNotificationLevel(NotificationLevel.Urgent);

            state.UpdateNotificationLevel(NotificationLevel.Gentle);

            state.CurrentLevel.Should().Be(NotificationLevel.Urgent);
        }

        [Test]
        public void WhenSameLevel_DoesNotChange()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.UpdateNotificationLevel(NotificationLevel.Moderate);

            state.UpdateNotificationLevel(NotificationLevel.Moderate);

            state.CurrentLevel.Should().Be(NotificationLevel.Moderate);
        }

        [Test]
        public void CanEscalateThroughAllLevels()
        {
            var state = new MeetingState(CreateTestMeeting());

            state.UpdateNotificationLevel(NotificationLevel.Gentle);
            state.CurrentLevel.Should().Be(NotificationLevel.Gentle);

            state.UpdateNotificationLevel(NotificationLevel.Moderate);
            state.CurrentLevel.Should().Be(NotificationLevel.Moderate);

            state.UpdateNotificationLevel(NotificationLevel.Urgent);
            state.CurrentLevel.Should().Be(NotificationLevel.Urgent);

            state.UpdateNotificationLevel(NotificationLevel.Critical);
            state.CurrentLevel.Should().Be(NotificationLevel.Critical);
        }

        [Test]
        public void CannotDeescalateFromCritical()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.UpdateNotificationLevel(NotificationLevel.Critical);

            state.UpdateNotificationLevel(NotificationLevel.Urgent);
            state.UpdateNotificationLevel(NotificationLevel.Moderate);
            state.UpdateNotificationLevel(NotificationLevel.Gentle);
            state.UpdateNotificationLevel(NotificationLevel.None);

            state.CurrentLevel.Should().Be(NotificationLevel.Critical);
        }
    }

    [TestFixture]
    public sealed class AcknowledgeTests : MeetingStateTests
    {
        [Test]
        public void SetsIsAcknowledgedToTrue()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.IsAcknowledged.Should().BeFalse();

            state.Acknowledge();

            state.IsAcknowledged.Should().BeTrue();
        }

        [Test]
        public void ResetsNotificationLevelToNone()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.UpdateNotificationLevel(NotificationLevel.Critical);

            state.Acknowledge();

            state.CurrentLevel.Should().Be(NotificationLevel.None);
        }

        [Test]
        public void WhenAlreadyAcknowledged_RemainsAcknowledged()
        {
            var state = new MeetingState(CreateTestMeeting());
            state.Acknowledge();

            state.Acknowledge();

            state.IsAcknowledged.Should().BeTrue();
            state.CurrentLevel.Should().Be(NotificationLevel.None);
        }
    }

    [TestFixture]
    public sealed class UpdateLastNotificationTimeTests : MeetingStateTests
    {
        [Test]
        public void UpdatesTimestamp()
        {
            var state = new MeetingState(CreateTestMeeting());
            var timestamp = new DateTime(2025, 1, 15, 14, 30, 0);

            state.UpdateLastNotificationTime(timestamp);

            state.LastNotificationTime.Should().Be(timestamp);
        }

        [Test]
        public void CanBeUpdatedMultipleTimes()
        {
            var state = new MeetingState(CreateTestMeeting());
            var firstTimestamp = new DateTime(2025, 1, 15, 14, 0, 0);
            var secondTimestamp = new DateTime(2025, 1, 15, 14, 30, 0);

            state.UpdateLastNotificationTime(firstTimestamp);
            state.UpdateLastNotificationTime(secondTimestamp);

            state.LastNotificationTime.Should().Be(secondTimestamp);
        }
    }

    [TestFixture]
    public sealed class InitialStateTests : MeetingStateTests
    {
        [Test]
        public void Constructor_InitializesWithCorrectDefaults()
        {
            var meeting = CreateTestMeeting();
            var state = new MeetingState(meeting);

            state.Event.Should().BeSameAs(meeting);
            state.CurrentLevel.Should().Be(NotificationLevel.None);
            state.IsAcknowledged.Should().BeFalse();
            state.LastNotificationTime.Should().Be(DateTime.MinValue);
        }
    }
}
