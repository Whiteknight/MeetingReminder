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

            // Use UTC time throughout - local time conversion happens only in UI
            // TODO: Double-check this logic, we're mixing local time and UTC time in a weird way
            // What we want is for the local "today" bounds translated to UTC so items near midnight UTC are displayed in the correct day
            // "The end of the local calendar day should be derived more defensively, e.g. using DateTime.Today converted to UTC or DateTimeOffset."
            var localNow = _timeProvider.UtcNow.ToLocalTime();
            var query = new FetchCalendarEventsQuery(
                StartTime: _timeProvider.UtcNow,
                EndTime: new DateTime(localNow.Year, localNow.Month, localNow.Day, 23, 59, 59, DateTimeKind.Local).ToUniversalTime());

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
