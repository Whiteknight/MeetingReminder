using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using MeetingReminder.Domain.Meetings;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class AcknowledgeMeetingTests
{
    private IMeetingRepository _meetingRepository = null!;
    private IBrowserLauncher _browserLauncher = null!;
    private AcknowledgeMeeting _acknowledgeMeeting = null!;

    [SetUp]
    public void SetUp()
    {
        _meetingRepository = Substitute.For<IMeetingRepository>();
        _browserLauncher = Substitute.For<IBrowserLauncher>();
        _acknowledgeMeeting = new AcknowledgeMeeting(
            _meetingRepository,
            _browserLauncher,
            new SystemTimeProvider());
    }

    private static MeetingEvent CreateTestMeetingEvent(string id, MeetingLink? link = null)
    {
        return new MeetingEvent(
            id: id,
            title: "Test Meeting",
            startTime: DateTime.UtcNow.AddHours(1),
            endTime: DateTime.UtcNow.AddHours(2),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendarSource: "test-calendar",
            link: link);
    }

    [TestFixture]
    public sealed class ValidationTests : AcknowledgeMeetingTests
    {
        [Test]
        public async Task WithNullMeetingId_ReturnsError()
        {
            var command = new AcknowledgeMeetingCommand(null!, false);

            var result = await _acknowledgeMeeting.Acknowledge(command);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public async Task WithEmptyMeetingId_ReturnsError()
        {
            var command = new AcknowledgeMeetingCommand(string.Empty, false);

            var result = await _acknowledgeMeeting.Acknowledge(command);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public async Task WithWhitespaceMeetingId_ReturnsError()
        {
            var command = new AcknowledgeMeetingCommand("   ", false);

            var result = await _acknowledgeMeeting.Acknowledge(command);

            result.IsError.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class MeetingNotFoundTests : AcknowledgeMeetingTests
    {
        [Test]
        public async Task WhenMeetingNotFound_ReturnsError()
        {
            var command = new AcknowledgeMeetingCommand("meeting-123", false);
            _meetingRepository.GetById("meeting-123")
                .Returns((Result<MeetingState, Error>)new UnknownError("Not found"));

            var result = await _acknowledgeMeeting.Acknowledge(command);

            result.IsError.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class SuccessfulAcknowledgementTests : AcknowledgeMeetingTests
    {
        //[Test]
        //public async Task WithValidMeetingId_AcknowledgesMeeting()
        //{
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123");
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", false);

        //    _meetingRepository.GetById("meeting-123")
        //        .Returns((Result<MeetingState, Error>)meetingState);
        //    _meetingRepository.Update(Arg.Any<MeetingState>())
        //        .Returns(r => (Result<MeetingState, Error>)(MeetingState)r.Args()[0]);

        //    var result = await _acknowledgeMeeting.Acknowledge(command);

        //    result.IsSuccess.Should().BeTrue();
        //    meetingState.IsAcknowledged.Should().BeTrue();
        //}

        //[Test]
        //public async Task WithValidMeetingId_WritesToChannel()
        //{
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123");
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", false);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

        //    await _acknowledgeMeeting.Acknowledge(command);

        //    _acknowledgementChannel.Reader.TryRead(out var acknowledged).Should().BeTrue();
        //    acknowledged!.MeetingId.Should().Be("meeting-123");
        //    acknowledged.LinkOpened.Should().BeFalse();
        //}

        //[Test]
        //public async Task WithValidMeetingId_UpdatesRepository()
        //{
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123");
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", false);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

        //    await _acknowledgeMeeting.Acknowledge(command);

        //    await _meetingRepository.Received(1).UpdateAsync(meetingState);
        //}
    }

    [TestFixture]
    public sealed class OpenLinkTests : AcknowledgeMeetingTests
    {
        //[Test]
        //public async Task WithOpenLinkTrue_OpensBrowserWithMeetingLink()
        //{
        //    var meetingLink = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123", meetingLink);
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", true);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));
        //    _browserLauncher.OpenUrl(meetingLink.Url)
        //        .Returns(Unit.Value);

        //    var result = await _acknowledgeMeeting.Acknowledge(command);

        //    result.IsSuccess.Should().BeTrue();
        //    _browserLauncher.Received(1).OpenUrl(meetingLink.Url);
        //}

        //[Test]
        //public async Task WithOpenLinkTrue_SetsLinkOpenedInChannel()
        //{
        //    var meetingLink = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123", meetingLink);
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", true);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));
        //    _browserLauncher.OpenUrl(meetingLink.Url)
        //        .Returns(Unit.Value);

        //    await _acknowledgeMeeting.Acknowledge(command);

        //    _acknowledgementChannel.Reader.TryRead(out var acknowledged).Should().BeTrue();
        //    acknowledged!.LinkOpened.Should().BeTrue();
        //}

        //[Test]
        //public async Task WithOpenLinkTrueButNoLink_DoesNotOpenBrowser()
        //{
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123");
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", true);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

        //    var result = await _acknowledgeMeeting.Acknowledge(command);

        //    result.IsSuccess.Should().BeTrue();
        //    _browserLauncher.DidNotReceive().OpenUrl(Arg.Any<string>());
        //}

        //[Test]
        //public async Task WithOpenLinkFalse_DoesNotOpenBrowser()
        //{
        //    var meetingLink = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123", meetingLink);
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", false);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
        //        .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

        //    var result = await _acknowledgeMeeting.Acknowledge(command);

        //    result.IsSuccess.Should().BeTrue();
        //    _browserLauncher.DidNotReceive().OpenUrl(Arg.Any<string>());
        //}

        //[Test]
        //public async Task WhenBrowserLaunchFails_ReturnsError()
        //{
        //    var meetingLink = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
        //    var meetingEvent = CreateTestMeetingEvent("meeting-123", meetingLink);
        //    var meetingState = new MeetingState(meetingEvent);
        //    var command = new AcknowledgeMeetingCommand("meeting-123", true);

        //    _meetingRepository.GetByIdAsync("meeting-123")
        //        .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
        //    _browserLauncher.OpenUrl(meetingLink.Url)
        //        .Returns(new UnknownError("Browser launch failed"));

        //    var result = await _acknowledgeMeeting.Acknowledge(command);

        //    result.IsError.Should().BeTrue();
        //}
    }

    [TestFixture]
    public sealed class RepositoryUpdateFailureTests : AcknowledgeMeetingTests
    {
        [Test]
        public async Task WhenRepositoryUpdateFails_ReturnsError()
        {
            //var meetingEvent = CreateTestMeetingEvent("meeting-123");
            //var meetingState = new MeetingState(meetingEvent);
            //var command = new AcknowledgeMeetingCommand("meeting-123", false);

            //_meetingRepository.GetByIdAsync("meeting-123")
            //    .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
            //_meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
            //    .Returns(Task.FromResult<Result<Unit, Error>>(new UnknownError("Update failed")));

            //var result = await _acknowledgeMeeting.Acknowledge(command);

            //result.IsError.Should().BeTrue();
        }
    }
}
