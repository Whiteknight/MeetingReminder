using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Infrastructure.Calendars;

/// <summary>
/// Service that polls calendar sources at a configured interval and publishes updates
/// to a channel for consumption by other components.
/// Uses SemaphoreSlim to prevent overlapping polls.
/// </summary>
public class CalendarPollingService : ICalendarPollingService
{
    private readonly FetchCalendarEvents _fetchCalendarEvents;
    private readonly TimeSpan _pollingInterval;
    private readonly SemaphoreSlim _pollLock;
    private readonly ConsolidateIncomingMeetings _consolidateIncomingMeetings;
    private readonly ITimeProvider _timeProvider;
    private readonly PollingSchedule _pollingSchedule;

    private Timer? _timer;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the CalendarPollingService.
    /// </summary>
    /// <param name="fetchCalendarEvents">Use case for fetching calendar events</param>
    /// <param name="consolidateIncomingMeetings">Use case for consolidating incoming meeting data</param>
    /// <param name="configuration">Application configuration containing polling interval</param>
    /// <param name="timeProvider">Time provider for testability</param>
    /// <param name="pollingSchedule">Shared store updated with the next scheduled fetch time</param>
    public CalendarPollingService(
        FetchCalendarEvents fetchCalendarEvents,
        ConsolidateIncomingMeetings consolidateIncomingMeetings,
        IAppConfiguration configuration,
        ITimeProvider timeProvider,
        PollingSchedule pollingSchedule)
    {
        _fetchCalendarEvents = NotNull(fetchCalendarEvents);
        _pollingInterval = configuration?.PollingInterval ?? TimeSpan.FromMinutes(5);
        _consolidateIncomingMeetings = NotNull(consolidateIncomingMeetings);
        _timeProvider = timeProvider;
        _pollingSchedule = NotNull(pollingSchedule);
        _pollLock = new SemaphoreSlim(1, 1);

        if (_pollingInterval < TimeSpan.FromMinutes(1))
            throw new ArgumentException("Polling interval must be at least 1 minute", nameof(configuration));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_timer is not null)
            return Task.CompletedTask; // Already running

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Record when the first poll will fire (immediately = now)
        _pollingSchedule.SetNextFetchAt(_timeProvider.UtcNow);

        // Start timer with immediate first poll, then at configured interval
        _timer = new Timer(
            callback: _ =>
            {
                // Guard against accessing disposed CTS during shutdown
                if (_disposed || _cts is null)
                    return;

                try
                {
                    _ = PollInternalAsync(_cts.Token);
                }
                catch (ObjectDisposedException)
                {
                    // CTS was disposed during shutdown - ignore
                }
            },
            state: null,
            dueTime: TimeSpan.Zero,
            period: _pollingInterval);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_timer is null)
            return;

        _cts?.Cancel();

        // Stop the timer
        await _timer.DisposeAsync();
        _timer = null;

        // Wait for any in-progress poll to complete
        await _pollLock.WaitAsync();
        _pollLock.Release();
    }

    private async Task PollInternalAsync(CancellationToken cancellationToken)
    {
        // Try to acquire lock without waiting - skip if previous poll still running
        if (!await _pollLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            // Schedule the next fetch now that this one is starting.
            // This gives the UI an accurate countdown to the following cycle.
            _pollingSchedule.SetNextFetchAt(_timeProvider.UtcNow + _pollingInterval);

            var query = new FetchCalendarEventsQuery(
                StartTime: BuildLocalDayStartUtc(_timeProvider.UtcNow),
                EndTime: BuildLocalDayEndUtc(_timeProvider.UtcNow));

            var result = await _fetchCalendarEvents.Fetch(query, cancellationToken)
                .BindAsync(_consolidateIncomingMeetings.Consolidate, cancellationToken);

            // On failure, we don't update the channel - the UI will continue showing
            // the last known state. Errors are logged elsewhere.
        }
        finally
        {
            _pollLock.Release();
        }
    }

    /// <summary>
    /// Returns the UTC instant corresponding to local midnight (start of today) for the
    /// calendar day that contains <paramref name="utcNow"/> in the system local timezone.
    /// </summary>
    private static DateTime BuildLocalDayStartUtc(DateTime utcNow)
    {
        var localNow = new DateTimeOffset(utcNow, TimeSpan.Zero).ToLocalTime();
        var localMidnight = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, localNow.Offset);
        return localMidnight.UtcDateTime;
    }

    /// <summary>
    /// Returns the UTC instant corresponding to 23:59:59 local time (end of today) for the
    /// calendar day that contains <paramref name="utcNow"/> in the system local timezone.
    /// </summary>
    private static DateTime BuildLocalDayEndUtc(DateTime utcNow)
    {
        var localNow = new DateTimeOffset(utcNow, TimeSpan.Zero).ToLocalTime();
        var localEndOfDay = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 23, 59, 59, localNow.Offset);
        return localEndOfDay.UtcDateTime;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _timer?.Dispose();
            _cts?.Dispose();
            _pollLock.Dispose();
        }

        _disposed = true;
    }
}
