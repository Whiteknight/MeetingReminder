using AwesomeAssertions;
using MeetingReminder.Infrastructure.Calendars;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Calendars;

[TestFixture]
public class PollingScheduleTests
{
    private PollingSchedule _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new PollingSchedule();

    [Test]
    public void NextFetchAt_BeforeAnySet_IsNull()
    {
        _sut.NextFetchAt.Should().BeNull();
    }

    [Test]
    public void NextFetchAt_AfterSet_ReturnsSetValue()
    {
        var expected = new DateTime(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc);

        _sut.SetNextFetchAt(expected);

        _sut.NextFetchAt.Should().Be(expected);
    }

    [Test]
    public void NextFetchAt_AfterSet_HasUtcKind()
    {
        _sut.SetNextFetchAt(new DateTime(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc));

        _sut.NextFetchAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void SetNextFetchAt_WithLocalTime_ConvertsToUtc()
    {
        // Arrange: a local time whose UTC equivalent is known
        var localTime = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Local);
        var expectedUtc = localTime.ToUniversalTime();

        _sut.SetNextFetchAt(localTime);

        _sut.NextFetchAt.Should().Be(expectedUtc);
        _sut.NextFetchAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void SetNextFetchAt_CalledTwice_ReturnsLatestValue()
    {
        var first = new DateTime(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc);
        var second = new DateTime(2026, 8, 6, 14, 5, 0, DateTimeKind.Utc);

        _sut.SetNextFetchAt(first);
        _sut.SetNextFetchAt(second);

        _sut.NextFetchAt.Should().Be(second);
    }

    [Test]
    public void SetNextFetchAt_ConcurrentWrites_LastWriteWins()
    {
        // Concurrent writes should not corrupt the value (Interlocked guarantees atomicity).
        // We verify the result is one of the two written values, not a torn read.
        var valueA = new DateTime(2026, 8, 6, 14, 0, 0, DateTimeKind.Utc);
        var valueB = new DateTime(2026, 8, 6, 14, 5, 0, DateTimeKind.Utc);

        var t1 = Task.Run(() => { for (int i = 0; i < 10_000; i++) _sut.SetNextFetchAt(valueA); });
        var t2 = Task.Run(() => { for (int i = 0; i < 10_000; i++) _sut.SetNextFetchAt(valueB); });

        Task.WaitAll(t1, t2);

        var result = _sut.NextFetchAt;
        result.Should().NotBeNull();
        result.Should().BeOneOf(valueA, valueB);
    }
}
