using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Meetings;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Meetings;

/// <summary>
/// Tests for ConsolidateIncomingMeetings covering the full cycle through InMemoryMeetingRepository.
/// </summary>
[TestFixture]
public class ConsolidateIncomingMeetingsTests
{
    private static readonly CalendarName CalendarA = new("outlook");
    private static readonly CalendarName CalendarB = new("google");

    private static readonly DateTime BaseTime = new(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc);

    private InMemoryMeetingRepository _repository = null!;
    private ConsolidateIncomingMeetings _consolidate = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryMeetingRepository(Substitute.For<IChangeNotifier>());
        _consolidate = new ConsolidateIncomingMeetings(_repository);
    }

    private static MeetingEvent CreateEvent(string id, CalendarName calendar) =>
        MeetingEvent.Create(
            id: new MeetingId(calendar, id),
            title: $"Meeting {id}",
            startTime: BaseTime.AddHours(1),
            endTime: BaseTime.AddHours(2),
            description: string.Empty,
            location: string.Empty,
            isAllDay: false,
            calendar: calendar);

    private static IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>> Events(
        params (CalendarName Calendar, MeetingEvent[] Events)[] entries)
        => entries.ToDictionary(e => e.Calendar, e => (IReadOnlyList<MeetingEvent>)e.Events);

    [TestFixture]
    public sealed class AddTests : ConsolidateIncomingMeetingsTests
    {
        [Test]
        public async Task NewMeeting_IsAddedToRepository()
        {
            var meeting = CreateEvent("evt-1", CalendarA);

            await _consolidate.Consolidate(Events((CalendarA, [meeting])), CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().HaveCount(1);
            all[0].Event.Id.Should().Be(meeting.Id);
        }

        [Test]
        public async Task NewMeetingsFromTwoCalendars_BothAddedToRepository()
        {
            var meetingA = CreateEvent("a-1", CalendarA);
            var meetingB = CreateEvent("b-1", CalendarB);

            await _consolidate.Consolidate(
                Events((CalendarA, [meetingA]), (CalendarB, [meetingB])),
                CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().HaveCount(2);
        }
    }

    [TestFixture]
    public sealed class UpdateTests : ConsolidateIncomingMeetingsTests
    {
        [Test]
        public async Task ExistingMeeting_EventDataIsUpdated()
        {
            var original = CreateEvent("evt-1", CalendarA);
            _repository.Add(MeetingState.New(original));

            var updated = MeetingEvent.Create(
                id: original.Id,
                title: "Updated Title",
                startTime: BaseTime.AddHours(2),
                endTime: BaseTime.AddHours(3),
                description: string.Empty,
                location: string.Empty,
                isAllDay: false,
                calendar: CalendarA);

            await _consolidate.Consolidate(Events((CalendarA, [updated])), CancellationToken.None);

            var state = _repository.GetById(original.Id).GetValueOrDefault(default);
            state.Event.Title.Should().Be("Updated Title");
        }

        [Test]
        public async Task ExistingAcknowledgedMeeting_AcknowledgedFlagIsPreserved()
        {
            var meeting = CreateEvent("evt-1", CalendarA);
            var acknowledged = MeetingState.New(meeting).Acknowledge(BaseTime);
            _repository.Add(acknowledged);

            await _consolidate.Consolidate(Events((CalendarA, [meeting])), CancellationToken.None);

            var state = _repository.GetById(meeting.Id).GetValueOrDefault(default);
            state.IsAcknowledged.Should().BeTrue("acknowledged state must survive a calendar refresh");
        }

        [Test]
        public async Task ExistingMeetingWithEscalatedLevel_NotificationLevelIsPreserved()
        {
            var meeting = CreateEvent("evt-1", CalendarA);
            var escalated = MeetingState.New(meeting).UpdateNotificationLevel(NotificationLevel.Urgent, BaseTime);
            _repository.Add(escalated);

            await _consolidate.Consolidate(Events((CalendarA, [meeting])), CancellationToken.None);

            var state = _repository.GetById(meeting.Id).GetValueOrDefault(default);
            state.CurrentLevel.Should().Be(NotificationLevel.Urgent, "notification level must survive a calendar refresh");
        }
    }

    [TestFixture]
    public sealed class RemovalTests : ConsolidateIncomingMeetingsTests
    {
        [Test]
        public async Task MeetingAbsentFromFeed_IsRemovedFromRepository()
        {
            var staying = CreateEvent("evt-stay", CalendarA);
            var leaving = CreateEvent("evt-leave", CalendarA);
            _repository.Add(MeetingState.New(staying));
            _repository.Add(MeetingState.New(leaving));

            // Next poll: only 'staying' comes back
            await _consolidate.Consolidate(Events((CalendarA, [staying])), CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().HaveCount(1);
            all[0].Event.Id.Should().Be(staying.Id);
        }

        [Test]
        public async Task CalendarReturnsEmptyList_AllMeetingsForThatCalendarAreRemoved()
        {
            // This is the regression scenario: a calendar that has no more events today returns
            // an empty list. All its existing repository entries must be removed.
            var meetingA1 = CreateEvent("a-1", CalendarA);
            var meetingA2 = CreateEvent("a-2", CalendarA);
            var meetingB = CreateEvent("b-1", CalendarB);
            _repository.Add(MeetingState.New(meetingA1));
            _repository.Add(MeetingState.New(meetingA2));
            _repository.Add(MeetingState.New(meetingB));

            // Calendar A returns nothing; Calendar B still has a meeting
            await _consolidate.Consolidate(
                Events((CalendarA, []), (CalendarB, [meetingB])),
                CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().HaveCount(1);
            all[0].Event.Calendar.Should().Be(CalendarB);
        }

        [Test]
        public async Task AcknowledgedMeetingAbsentFromFeed_IsStillRemoved()
        {
            // Acknowledged meetings that no longer appear in the feed should be removed.
            // The calendar is authoritative; acknowledgement only suppresses notifications.
            var meeting = CreateEvent("evt-1", CalendarA);
            _repository.Add(MeetingState.New(meeting).Acknowledge(BaseTime));

            await _consolidate.Consolidate(Events((CalendarA, [])), CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().BeEmpty();
        }
    }

    [TestFixture]
    public sealed class MultiCalendarIsolationTests : ConsolidateIncomingMeetingsTests
    {
        [Test]
        public async Task CalendarNotInIncomingDictionary_ItsRepositoryEntriesAreUntouched()
        {
            // If only one calendar is polled in a given cycle (e.g. the other errored),
            // the other calendar's meetings must not be removed.
            var meetingA = CreateEvent("a-1", CalendarA);
            var meetingB = CreateEvent("b-1", CalendarB);
            _repository.Add(MeetingState.New(meetingA));
            _repository.Add(MeetingState.New(meetingB));

            // Only calendar B is present in the incoming dictionary (A errored)
            await _consolidate.Consolidate(Events((CalendarB, [meetingB])), CancellationToken.None);

            var all = _repository.GetAll().GetValueOrDefault([]);
            all.Should().HaveCount(2);
        }
    }
}
