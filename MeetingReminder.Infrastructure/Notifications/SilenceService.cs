using MeetingReminder.Domain;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Infrastructure.Notifications;

/// <summary>
/// Thread-safe implementation of ISilenceService.
/// Stores the silence expiry as UTC ticks via Interlocked so reads on the
/// notification thread always see writes made on the UI thread without a lock.
/// long.MinValue is used as the sentinel meaning "not silenced".
/// </summary>
public sealed class SilenceService : ISilenceService
{
    private const long _notSilenced = long.MinValue;

    private readonly ITimeProvider _time;
    private long _silencedUntilTicks = _notSilenced;

    public SilenceService(ITimeProvider time)
    {
        _time = time;
    }

    /// <inheritdoc />
    public bool IsActive
    {
        get
        {
            var ticks = Interlocked.Read(ref _silencedUntilTicks);
            return ticks != _notSilenced && _time.UtcNow.Ticks < ticks;
        }
    }

    /// <inheritdoc />
    public DateTime? SilencedUntil
    {
        get
        {
            var ticks = Interlocked.Read(ref _silencedUntilTicks);
            return ticks == _notSilenced ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <inheritdoc />
    public void Activate(DateTime until)
    {
        Interlocked.Exchange(ref _silencedUntilTicks, until.ToUniversalTime().Ticks);
    }

    /// <inheritdoc />
    public void Deactivate()
    {
        Interlocked.Exchange(ref _silencedUntilTicks, _notSilenced);
    }
}
