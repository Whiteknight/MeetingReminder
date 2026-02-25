//using AwesomeAssertions;
//using MeetingReminder.Application.UseCases;
//using MeetingReminder.ConsoleTui.Services;
//using MeetingReminder.Domain;
//using MeetingReminder.Domain.Browsers;
//using MeetingReminder.Domain.Calendars;
//using MeetingReminder.Domain.Configuration;
//using MeetingReminder.Domain.Input;
//using MeetingReminder.Domain.Meetings;
//using MeetingReminder.Domain.Notifications;
//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using NSubstitute;
//using NUnit.Framework;
//using System.Threading.Channels;

//namespace MeetingReminder.ConsoleTui.Tests.Services;

//[TestFixture]
//public class TuiCommandHandlingTests
//{
//    private MeetingReminderTuiService _tuiService = null!;
//    private IMeetingRepository _meetingRepository = null!;
//    private IBrowserLauncher _browserLauncher = null!;
//    private IHostApplicationLifetime _applicationLifetime = null!;
//    private AcknowledgeMeeting _acknowledgeMeeting = null!;

//    [SetUp]
//    public void SetUp()
//    {
//        var calendarChannel = Channel.CreateUnbounded<CalendarEventsUpdated>();
//        var notificationChannel = Channel.CreateUnbounded<NotificationStateChanged>();
//        _acknowledgementChannel = Channel.CreateUnbounded<MeetingAcknowledged>();

//        var configuration = Substitute.For<IAppConfiguration>();
//        configuration.PollingInterval.Returns(TimeSpan.FromMinutes(5));

//        var timeProvider = Substitute.For<ITimeProvider>();
//        timeProvider.UtcNow.Returns(DateTime.UtcNow);

//        _meetingRepository = Substitute.For<IMeetingRepository>();
//        _browserLauncher = Substitute.For<IBrowserLauncher>();
//        _acknowledgeMeeting = new AcknowledgeMeeting(
//            _meetingRepository, _browserLauncher, _acknowledgementChannel.Writer);

//        _applicationLifetime = Substitute.For<IHostApplicationLifetime>();

//        var keyboardHandler = Substitute.For<IKeyboardInputHandler>();
//        var logger = Substitute.For<ILogger<MeetingReminderTuiService>>();

//        _tuiService = new MeetingReminderTuiService(
//            calendarChannel.Reader,
//            notificationChannel.Reader,
//            keyboardHandler,
//            _acknowledgeMeeting,
//            _applicationLifetime,
//            configuration,
//            timeProvider,
//            logger);
//    }

//    private static MeetingEvent CreateMeeting(
//        string id, DateTime? startTime = null, MeetingLink? link = null)
//    {
//        return new MeetingEvent(
//            id: id,
//            title: $"Test Meeting {id}",
//            startTime: startTime ?? DateTime.UtcNow.AddHours(1),
//            endTime: (startTime ?? DateTime.UtcNow.AddHours(1)).AddHours(1),
//            description: "Test description",
//            location: "Test location",
//            isAllDay: false,
//            calendarSource: "test-calendar",
//            link: link);
//    }

//    private void PopulateMeetings(params MeetingEvent[] meetings)
//        => _tuiService.SetEventsForTesting(meetings.ToList());

//    [TestFixture]
//    public sealed class NavigationTests : TuiCommandHandlingTests
//    {
//        [Test]
//        public async Task NavigateDown_WithNoSelection_SelectsFirstMeeting()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(0);
//        }

//        [Test]
//        public async Task NavigateDown_Twice_SelectsSecondMeeting()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(1);
//        }

//        [Test]
//        public async Task NavigateDown_AtLastMeeting_StaysAtLast()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(1);
//        }

//        [Test]
//        public async Task NavigateDown_WithMoreThanFiveEvents_ClampsToFifthRow()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)),
//                CreateMeeting("m3", DateTime.UtcNow.AddHours(3)),
//                CreateMeeting("m4", DateTime.UtcNow.AddHours(4)),
//                CreateMeeting("m5", DateTime.UtcNow.AddHours(5)),
//                CreateMeeting("m6", DateTime.UtcNow.AddHours(6)),
//                CreateMeeting("m7", DateTime.UtcNow.AddHours(7)));

//            for (var i = 0; i < 10; i++)
//                await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(4);
//        }

//        [Test]
//        public async Task NavigateDown_WithExactlyFiveEvents_ClampsToLastVisible()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)),
//                CreateMeeting("m3", DateTime.UtcNow.AddHours(3)),
//                CreateMeeting("m4", DateTime.UtcNow.AddHours(4)),
//                CreateMeeting("m5", DateTime.UtcNow.AddHours(5)));

//            for (var i = 0; i < 8; i++)
//                await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(4);
//        }

//        [Test]
//        public async Task NavigateUp_WithNoSelection_SelectsFirstMeeting()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateUp(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(0);
//        }

//        [Test]
//        public async Task NavigateUp_FromSecond_SelectsFirst()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateUp(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(0);
//        }

//        [Test]
//        public async Task NavigateUp_AtFirst_StaysAtFirst()
//        {
//            PopulateMeetings(
//                CreateMeeting("m1", DateTime.UtcNow.AddHours(1)),
//                CreateMeeting("m2", DateTime.UtcNow.AddHours(2)));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateUp(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(0);
//        }

//        [Test]
//        public async Task Navigate_WithNoMeetings_DoesNotThrow()
//        {
//            await _tuiService.Invoking(s =>
//                s.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None))
//                .Should().NotThrowAsync();

//            await _tuiService.Invoking(s =>
//                s.HandleCommandForTesting(new InputCommand.NavigateUp(), CancellationToken.None))
//                .Should().NotThrowAsync();
//        }
//    }

//    [TestFixture]
//    public sealed class AcknowledgeTests : TuiCommandHandlingTests
//    {
//        [Test]
//        public async Task Acknowledge_WithSelectedMeeting_AcknowledgesMeeting()
//        {
//            var meeting = CreateMeeting("m-123", DateTime.UtcNow.AddHours(1));
//            var meetingState = new MeetingState(meeting);
//            PopulateMeetings(meeting);

//            _meetingRepository.GetByIdAsync("m-123")
//                .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
//            _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
//                .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.Acknowledge(), CancellationToken.None);

//            meetingState.IsAcknowledged.Should().BeTrue();
//        }

//        [Test]
//        public async Task Acknowledge_DoesNotOpenLink()
//        {
//            var link = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
//            var meeting = CreateMeeting("m-123", DateTime.UtcNow.AddHours(1), link);
//            var meetingState = new MeetingState(meeting);
//            PopulateMeetings(meeting);

//            _meetingRepository.GetByIdAsync("m-123")
//                .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
//            _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
//                .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.Acknowledge(), CancellationToken.None);

//            _browserLauncher.DidNotReceive().OpenUrl(Arg.Any<string>());
//        }

//        [Test]
//        public async Task Acknowledge_WithNoMeetings_DoesNotThrow()
//        {
//            await _tuiService.Invoking(s =>
//                s.HandleCommandForTesting(new InputCommand.Acknowledge(), CancellationToken.None))
//                .Should().NotThrowAsync();
//        }
//    }

//    [TestFixture]
//    public sealed class OpenAndAcknowledgeTests : TuiCommandHandlingTests
//    {
//        [Test]
//        public async Task OpenAndAcknowledge_WithLink_OpensLinkAndAcknowledges()
//        {
//            var link = new GoogleMeetLink("https://meet.google.com/abc-defg-hij");
//            var meeting = CreateMeeting("m-123", DateTime.UtcNow.AddHours(1), link);
//            var meetingState = new MeetingState(meeting);
//            PopulateMeetings(meeting);

//            _meetingRepository.GetByIdAsync("m-123")
//                .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
//            _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
//                .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));
//            _browserLauncher.OpenUrl(link.Url).Returns(Unit.Value);

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.OpenAndAcknowledge(), CancellationToken.None);

//            _browserLauncher.Received(1).OpenUrl(link.Url);
//            meetingState.IsAcknowledged.Should().BeTrue();
//        }

//        [Test]
//        public async Task OpenAndAcknowledge_WithoutLink_AcknowledgesWithoutBrowser()
//        {
//            var meeting = CreateMeeting("m-123", DateTime.UtcNow.AddHours(1));
//            var meetingState = new MeetingState(meeting);
//            PopulateMeetings(meeting);

//            _meetingRepository.GetByIdAsync("m-123")
//                .Returns(Task.FromResult<Result<MeetingState, Error>>(meetingState));
//            _meetingRepository.UpdateAsync(Arg.Any<MeetingState>())
//                .Returns(Task.FromResult<Result<Unit, Error>>(Unit.Value));

//            await _tuiService.HandleCommandForTesting(new InputCommand.NavigateDown(), CancellationToken.None);
//            await _tuiService.HandleCommandForTesting(new InputCommand.OpenAndAcknowledge(), CancellationToken.None);

//            _browserLauncher.DidNotReceive().OpenUrl(Arg.Any<string>());
//            meetingState.IsAcknowledged.Should().BeTrue();
//        }

//        [Test]
//        public async Task OpenAndAcknowledge_WithNoMeetings_DoesNotThrow()
//        {
//            await _tuiService.Invoking(s =>
//                s.HandleCommandForTesting(new InputCommand.OpenAndAcknowledge(), CancellationToken.None))
//                .Should().NotThrowAsync();
//        }
//    }

//    [TestFixture]
//    public sealed class QuitTests : TuiCommandHandlingTests
//    {
//        [Test]
//        public async Task Quit_StopsApplication()
//        {
//            await _tuiService.HandleCommandForTesting(new InputCommand.Quit(), CancellationToken.None);
//            _applicationLifetime.Received(1).StopApplication();
//        }
//    }

//    [TestFixture]
//    public sealed class NoneCommandTests : TuiCommandHandlingTests
//    {
//        [Test]
//        public async Task None_DoesNotAffectState()
//        {
//            var meeting = CreateMeeting("m-123", DateTime.UtcNow.AddHours(1));
//            PopulateMeetings(meeting);

//            var initialIndex = _tuiService.GetSelectedIndex();

//            await _tuiService.HandleCommandForTesting(new InputCommand.None(), CancellationToken.None);

//            _tuiService.GetSelectedIndex().Should().Be(initialIndex);
//            _applicationLifetime.DidNotReceive().StopApplication();
//        }
//    }
//}
