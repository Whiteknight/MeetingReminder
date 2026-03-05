using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
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
    private readonly IMeetingRepository _meetings;
    private readonly ITimeProvider _timeProvider;

    private Timer? _timer;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the CalendarPollingService.
    /// </summary>
    /// <param name="fetchCalendarEvents">Use case for fetching calendar events</param>
    /// <param name="meetings"></param>
    /// <param name="configuration">Application configuration containing polling interval</param>
    /// <param name="timeProvider">Optional time provider for testing (defaults to system time)</param>
    public CalendarPollingService(
        FetchCalendarEvents fetchCalendarEvents,
        IMeetingRepository meetings,
        IAppConfiguration configuration,
        ITimeProvider timeProvider)
    {
        _fetchCalendarEvents = NotNull(fetchCalendarEvents);
        _pollingInterval = configuration?.PollingInterval ?? TimeSpan.FromMinutes(5);
        _meetings = meetings;
        _timeProvider = timeProvider;
        _pollLock = new SemaphoreSlim(1, 1);

        if (_pollingInterval < TimeSpan.FromMinutes(1))
            throw new ArgumentException("Polling interval must be at least 1 minute", nameof(configuration));
    }

    /// <inheritdoc />
    public bool IsRunning => _timer is not null && !_disposed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_timer is not null)
            return Task.CompletedTask; // Already running

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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

    /// <inheritdoc />
    public async Task PollNowAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await PollInternalAsync(cancellationToken);
    }

    private async Task PollInternalAsync(CancellationToken cancellationToken)
    {
        // Try to acquire lock without waiting - skip if previous poll still running
        if (!await _pollLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            // Use UTC time throughout - local time conversion happens only in UI
            var now = _timeProvider.UtcNow;
            var localNow = now.ToLocalTime();
            var query = new FetchCalendarEventsQuery(
                StartTime: now,
                EndTime: new DateTime(localNow.Year, localNow.Month, localNow.Day, 23, 59, 59, DateTimeKind.Local).ToUniversalTime());

            var result = await _fetchCalendarEvents.Fetch(query, cancellationToken);
            if (result.IsSuccess)
            {
                var events = result.Match(e => e, _ => new Dictionary<CalendarName, IReadOnlyList<MeetingEvent>>());
                await ProcessFetchedEventsAsync(events, now, cancellationToken);
            }
            // On failure, we don't update the channel - the UI will continue showing
            // the last known state. Errors are logged elsewhere.
        }
        finally
        {
            _pollLock.Release();
        }
    }

    private async Task ProcessFetchedEventsAsync(
        IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>> events,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        foreach (var (calendarSource, incomingMeetings) in events)
        {
            var existingResult = _meetings.GetAllByCalendar(calendarSource);
            if (!existingResult.IsSuccess)
                continue;
            var existing = existingResult.GetValueOrDefault([]).ToDictionary(e => e.Event.Id);

            foreach (var incoming in incomingMeetings)
            {
                if (!existing.ContainsKey(incoming.Id))
                {
                    _meetings.Add(MeetingState.New(incoming));
                    continue;
                }

                _meetings.Update(existing[incoming.Id].UpdateEvent(incoming));
                existing.Remove(incoming.Id);
            }

            foreach (var remaining in existing.Values)
                _meetings.Remove(remaining.Event.Id);
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
