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
        return MeetingEvent.Create(
            id: new MeetingId("TestCalendar", "test-id"),
            title: "Test Meeting",
            startTime: DateTime.Now.AddHours(1),
            endTime: DateTime.Now.AddHours(2),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendar: "TestCalendar");
    }

    [TestFixture]
    public sealed class UpdateNotificationLevelTests : MeetingStateTests
    {
        [Test]
        public void WhenEscalating_UpdatesLevel()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state.CurrentLevel.Should().Be(NotificationLevel.None);

            state = state.UpdateNotificationLevel(NotificationLevel.Gentle, DateTime.UtcNow);

            state.CurrentLevel.Should().Be(NotificationLevel.Gentle);
        }

        [Test]
        public void WhenDeescalating_DoesNotUpdateLevel()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state = state.UpdateNotificationLevel(NotificationLevel.Urgent, DateTime.UtcNow);

            state = state.UpdateNotificationLevel(NotificationLevel.Gentle, DateTime.UtcNow);

            state.CurrentLevel.Should().Be(NotificationLevel.Urgent);
        }

        [Test]
        public void WhenSameLevel_DoesNotChange()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state = state.UpdateNotificationLevel(NotificationLevel.Moderate, DateTime.UtcNow);

            state = state.UpdateNotificationLevel(NotificationLevel.Moderate, DateTime.UtcNow);

            state.CurrentLevel.Should().Be(NotificationLevel.Moderate);
        }

        [Test]
        public void CanEscalateThroughAllLevels()
        {
            var state = MeetingState.New(CreateTestMeeting());

            state = state.UpdateNotificationLevel(NotificationLevel.Gentle, DateTime.UtcNow);
            state.CurrentLevel.Should().Be(NotificationLevel.Gentle);

            state = state.UpdateNotificationLevel(NotificationLevel.Moderate, DateTime.UtcNow);
            state.CurrentLevel.Should().Be(NotificationLevel.Moderate);

            state = state.UpdateNotificationLevel(NotificationLevel.Urgent, DateTime.UtcNow);
            state.CurrentLevel.Should().Be(NotificationLevel.Urgent);

            state = state.UpdateNotificationLevel(NotificationLevel.Critical, DateTime.UtcNow);
            state.CurrentLevel.Should().Be(NotificationLevel.Critical);
        }

        [Test]
        public void CannotDeescalateFromCritical()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state = state.UpdateNotificationLevel(NotificationLevel.Critical, DateTime.UtcNow);
            state = state.UpdateNotificationLevel(NotificationLevel.Urgent, DateTime.UtcNow);
            state = state.UpdateNotificationLevel(NotificationLevel.Moderate, DateTime.UtcNow);
            state = state.UpdateNotificationLevel(NotificationLevel.Gentle, DateTime.UtcNow);
            state = state.UpdateNotificationLevel(NotificationLevel.None, DateTime.UtcNow);

            state.CurrentLevel.Should().Be(NotificationLevel.Critical);
        }
    }

    [TestFixture]
    public sealed class AcknowledgeTests : MeetingStateTests
    {
        [Test]
        public void SetsIsAcknowledgedToTrue()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state.IsAcknowledged.Should().BeFalse();

            state = state.Acknowledge(DateTime.UtcNow);

            state.IsAcknowledged.Should().BeTrue();
        }

        [Test]
        public void ResetsNotificationLevelToNone()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state = state.UpdateNotificationLevel(NotificationLevel.Critical, DateTime.UtcNow);

            state = state.Acknowledge(DateTime.UtcNow);

            state.CurrentLevel.Should().Be(NotificationLevel.None);
        }

        [Test]
        public void WhenAlreadyAcknowledged_RemainsAcknowledged()
        {
            var state = MeetingState.New(CreateTestMeeting());
            state = state.Acknowledge(DateTime.UtcNow);

            state = state.Acknowledge(DateTime.UtcNow);

            state.IsAcknowledged.Should().BeTrue();
            state.CurrentLevel.Should().Be(NotificationLevel.None);
        }
    }

    [TestFixture]
    public sealed class InitialStateTests : MeetingStateTests
    {
        [Test]
        public void Constructor_InitializesWithCorrectDefaults()
        {
            var meeting = CreateTestMeeting();
            var state = MeetingState.New(meeting);

            state.Event.Should().BeSameAs(meeting);
            state.CurrentLevel.Should().Be(NotificationLevel.None);
            state.IsAcknowledged.Should().BeFalse();
            state.LastNotificationTime.Should().Be(DateTime.MinValue);
        }
    }
}
