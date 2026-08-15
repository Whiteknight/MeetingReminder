using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class UnacknowledgeMeetingTests
{
    private IMeetingRepository _meetingRepository = null!;
    private UnacknowledgeMeeting _unacknowledgeMeeting = null!;

    [SetUp]
    public void SetUp()
    {
        _meetingRepository = Substitute.For<IMeetingRepository>();
        _unacknowledgeMeeting = new UnacknowledgeMeeting(_meetingRepository);
    }

    private static MeetingEvent CreateTestMeetingEvent(string id)
        => MeetingEvent.Create(
            id: new MeetingId("test-calendar", id),
            title: "Test Meeting",
            startTime: DateTime.UtcNow.AddHours(1),
            endTime: DateTime.UtcNow.AddHours(2),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendar: "test-calendar");

    private static MeetingState AcknowledgedState(string id)
    {
        var meeting = CreateTestMeetingEvent(id);
        return MeetingState.New(meeting)
            .UpdateNotificationLevel(NotificationLevel.Urgent, DateTime.UtcNow)
            .Acknowledge(DateTime.UtcNow);
    }

    [TestFixture]
    public sealed class ValidationTests : UnacknowledgeMeetingTests
    {
        [Test]
        public void WithInvalidMeetingId_ReturnsError()
        {
            var command = new UnacknowledgeMeetingCommand(new MeetingId("", ""));

            var result = _unacknowledgeMeeting.Unacknowledge(command);

            result.IsError.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class MeetingNotFoundTests : UnacknowledgeMeetingTests
    {
        [Test]
        public void WhenMeetingNotFound_ReturnsError()
        {
            var id = new MeetingId("test-calendar", "missing-id");
            var command = new UnacknowledgeMeetingCommand(id);
            _meetingRepository.GetById(id)
                .Returns(new UnknownError("Meeting not found"));

            var result = _unacknowledgeMeeting.Unacknowledge(command);

            result.IsError.Should().BeTrue();
            result.Match(_ => string.Empty, e => e.Message)
                .Should().Contain("not found");
        }
    }

    [TestFixture]
    public sealed class SuccessfulUnacknowledgementTests : UnacknowledgeMeetingTests
    {
        [Test]
        public void WithAcknowledgedMeeting_SetsIsAcknowledgedToFalse()
        {
            var id = new MeetingId("test-calendar", "meeting-123");
            var state = AcknowledgedState("meeting-123");
            var command = new UnacknowledgeMeetingCommand(id);

            _meetingRepository.GetById(id).Returns(state);
            _meetingRepository.Update(Arg.Any<MeetingState>())
                .Returns(args => (MeetingState)args[0]);

            var result = _unacknowledgeMeeting.Unacknowledge(command);

            result.IsSuccess.Should().BeTrue();
            result.Match(s => s.IsAcknowledged, _ => true).Should().BeFalse();
        }

        [Test]
        public void WithAcknowledgedMeeting_ResetsNotificationLevelToNone()
        {
            var id = new MeetingId("test-calendar", "meeting-123");
            var state = AcknowledgedState("meeting-123");
            var command = new UnacknowledgeMeetingCommand(id);

            _meetingRepository.GetById(id).Returns(state);
            _meetingRepository.Update(Arg.Any<MeetingState>())
                .Returns(args => (MeetingState)args[0]);

            var result = _unacknowledgeMeeting.Unacknowledge(command);

            result.Match(s => s.CurrentLevel, _ => NotificationLevel.Critical)
                .Should().Be(NotificationLevel.None);
        }

        [Test]
        public void CallsRepositoryUpdateWithUnacknowledgedState()
        {
            var id = new MeetingId("test-calendar", "meeting-123");
            var state = AcknowledgedState("meeting-123");
            var command = new UnacknowledgeMeetingCommand(id);

            _meetingRepository.GetById(id).Returns(state);
            _meetingRepository.Update(Arg.Any<MeetingState>())
                .Returns(args => (MeetingState)args[0]);

            _unacknowledgeMeeting.Unacknowledge(command);

            _meetingRepository.Received(1).Update(Arg.Is<MeetingState>(s => !s.IsAcknowledged));
        }
    }

    [TestFixture]
    public sealed class RepositoryUpdateFailureTests : UnacknowledgeMeetingTests
    {
        [Test]
        public void WhenRepositoryUpdateFails_ReturnsError()
        {
            var id = new MeetingId("test-calendar", "meeting-123");
            var state = AcknowledgedState("meeting-123");
            var command = new UnacknowledgeMeetingCommand(id);

            _meetingRepository.GetById(id).Returns(state);
            _meetingRepository.Update(Arg.Any<MeetingState>())
                .Returns(new UnknownError("Update failed"));

            var result = _unacknowledgeMeeting.Unacknowledge(command);

            result.IsError.Should().BeTrue();
        }
    }
}
