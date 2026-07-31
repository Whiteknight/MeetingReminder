using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class FetchCalendarEventsTests
{
    private static readonly CalendarName SourceA = new("calendar-a");
    private static readonly CalendarName SourceB = new("calendar-b");

    private static readonly DateTime StartTime = new(2026, 7, 31, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EndTime = new(2026, 7, 31, 23, 59, 59, DateTimeKind.Utc);

    private static RawCalendarEvent CreateRawEvent(string id, CalendarName calendar) =>
        new(
            Id: id,
            Title: $"Meeting {id}",
            StartTime: StartTime.AddHours(1),
            EndTime: StartTime.AddHours(2),
            Description: string.Empty,
            Location: string.Empty,
            IsAllDay: false,
            Calendar: calendar);

    private static FetchCalendarEventsQuery DefaultQuery => new(StartTime, EndTime);

    private sealed class StubCalendarSource : ICalendarSource
    {
        private readonly IReadOnlyList<RawCalendarEvent>? _events;
        private readonly CalendarError? _error;

        public StubCalendarSource(CalendarName name, IReadOnlyList<RawCalendarEvent> events)
        {
            Name = name;
            _events = events;
        }

        public StubCalendarSource(CalendarName name, CalendarError error)
        {
            Name = name;
            _error = error;
        }

        public CalendarName Name { get; }

        public Task<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>> FetchEvents(
            DateTime startTime,
            DateTime endTime,
            CancellationToken cancellationToken)
        {
            if (_error is not null)
                return Task.FromResult<Result<IReadOnlyList<RawCalendarEvent>, CalendarError>>(_error);

            return Task.FromResult(Result.FromValue<IReadOnlyList<RawCalendarEvent>, CalendarError>(_events!));
        }
    }

    [TestFixture]
    public sealed class NoSourcesTests : FetchCalendarEventsTests
    {
        [Test]
        public async Task WithNoSources_ReturnsError()
        {
            var useCase = new FetchCalendarEvents([]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public void Constructor_WithNullSources_ThrowsArgumentNullException()
        {
            var act = () => new FetchCalendarEvents(null!);

            act.Should().Throw<ArgumentNullException>();
        }
    }

    [TestFixture]
    public sealed class SingleSourceTests : FetchCalendarEventsTests
    {
        [Test]
        public async Task WithEvents_ReturnsSuccessWithCalendarInDictionary()
        {
            var events = new[] { CreateRawEvent("evt-1", SourceA) };
            var source = new StubCalendarSource(SourceA, events);
            var useCase = new FetchCalendarEvents([source]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceA);
            dict[SourceA].Should().HaveCount(1);
        }

        [Test]
        public async Task WithEmptyEventList_ReturnsSuccessWithCalendarPresentButEmpty()
        {
            // A source returning an empty list is not an error — it means "no events today".
            // The calendar must still appear in the result so ConsolidateIncomingMeetings
            // can remove any stale repository entries for that calendar.
            var source = new StubCalendarSource(SourceA, Array.Empty<RawCalendarEvent>());
            var useCase = new FetchCalendarEvents([source]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceA);
            dict[SourceA].Should().BeEmpty();
        }

        [Test]
        public async Task WithError_ReturnsError()
        {
            var error = CalendarError.NetworkError(SourceA, "connection refused");
            var source = new StubCalendarSource(SourceA, error);
            var useCase = new FetchCalendarEvents([source]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsError.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class MultipleSourceTests : FetchCalendarEventsTests
    {
        [Test]
        public async Task BothSourcesHaveEvents_BothCalendarsInDictionary()
        {
            var sourceA = new StubCalendarSource(SourceA, new[] { CreateRawEvent("a-1", SourceA) });
            var sourceB = new StubCalendarSource(SourceB, new[] { CreateRawEvent("b-1", SourceB) });
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceA);
            dict.Should().ContainKey(SourceB);
        }

        [Test]
        public async Task OneSourceEmpty_OneSourceHasEvents_BothCalendarsInDictionary()
        {
            // This is the core regression case: source A returns nothing (end of day),
            // source B returns events. Source A must still appear in the result so that
            // stale meetings from source A are removed from the repository.
            var sourceA = new StubCalendarSource(SourceA, Array.Empty<RawCalendarEvent>());
            var sourceB = new StubCalendarSource(SourceB, new[] { CreateRawEvent("b-1", SourceB) });
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceA);
            dict[SourceA].Should().BeEmpty();
            dict.Should().ContainKey(SourceB);
            dict[SourceB].Should().HaveCount(1);
        }

        [Test]
        public async Task BothSourcesEmpty_ReturnsSuccessWithBothCalendarsPresent()
        {
            var sourceA = new StubCalendarSource(SourceA, Array.Empty<RawCalendarEvent>());
            var sourceB = new StubCalendarSource(SourceB, Array.Empty<RawCalendarEvent>());
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceA);
            dict.Should().ContainKey(SourceB);
        }

        [Test]
        public async Task OneSourceErrors_OneSourceHasEvents_SucceedsWithSuccessfulCalendar()
        {
            var sourceA = new StubCalendarSource(SourceA, CalendarError.NetworkError(SourceA, "timeout"));
            var sourceB = new StubCalendarSource(SourceB, new[] { CreateRawEvent("b-1", SourceB) });
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().NotContainKey(SourceA);
            dict.Should().ContainKey(SourceB);
        }

        [Test]
        public async Task OneSourceErrors_OneSourceEmpty_SucceedsWithEmptyCalendar()
        {
            // An errored source should NOT suppress the empty source from being included.
            var sourceA = new StubCalendarSource(SourceA, CalendarError.NetworkError(SourceA, "timeout"));
            var sourceB = new StubCalendarSource(SourceB, Array.Empty<RawCalendarEvent>());
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var dict = result.GetValueOrDefault(null!);
            dict.Should().ContainKey(SourceB);
            dict[SourceB].Should().BeEmpty();
        }

        [Test]
        public async Task AllSourcesError_ReturnsAggregateError()
        {
            var sourceA = new StubCalendarSource(SourceA, CalendarError.NetworkError(SourceA, "timeout"));
            var sourceB = new StubCalendarSource(SourceB, CalendarError.NetworkError(SourceB, "refused"));
            var useCase = new FetchCalendarEvents([sourceA, sourceB]);

            var result = await useCase.Fetch(DefaultQuery, CancellationToken.None);

            result.IsError.Should().BeTrue();
            var error = result.GetErrorOrDefault(null!);
            error.Message.Should().Contain("2");
        }
    }
}
