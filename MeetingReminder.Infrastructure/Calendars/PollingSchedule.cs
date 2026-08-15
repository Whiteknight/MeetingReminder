using MeetingReminder.Domain.Calendars;

namespace MeetingReminder.Infrastructure.Calendars;

/// <summary>
/// Thread-safe implementation of IPollingSchedule.
/// The polling thread writes via <see cref="SetNextFetchAt"/>.
/// The UI thread reads via <see cref="NextFetchAt"/>.
/// A backing long stores ticks so Interlocked operations can be used without locks.
/// </summary>
public sealed class PollingSchedule : IPollingSchedule
{
    // 0 = not yet set (DateTime(0).Ticks == 0, which is year 0001, never a real fetch time)
    private long _nextFetchAtTicks;

    /// <inheritdoc />
    public DateTime? NextFetchAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _nextFetchAtTicks);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Records the next expected fetch time. Called exclusively by the polling thread.
    /// </summary>
    /// <param name="nextFetchAt">UTC time of the next scheduled fetch.</param>
    public void SetNextFetchAt(DateTime nextFetchAt)
        => Interlocked.Exchange(ref _nextFetchAtTicks, nextFetchAt.ToUniversalTime().Ticks);
}
