using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Messaging;
using System.Threading.Channels;
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
    private readonly ChannelWriter<CalendarEventsUpdated> _calendarChannel;
    private readonly TimeSpan _pollingInterval;
    private readonly SemaphoreSlim _pollLock = new(1, 1);
    private readonly ITimeProvider _timeProvider;

    private Timer? _timer;
    private CancellationTokenSource? _cts;
    private Dictionary<string, MeetingEvent> _lastKnownEvents = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the CalendarPollingService.
    /// </summary>
    /// <param name="fetchCalendarEvents">Use case for fetching calendar events</param>
    /// <param name="calendarChannel">Channel to write calendar updates to</param>
    /// <param name="configuration">Application configuration containing polling interval</param>
    /// <param name="timeProvider">Optional time provider for testing (defaults to system time)</param>
    public CalendarPollingService(
        FetchCalendarEvents fetchCalendarEvents,
        ChannelWriter<CalendarEventsUpdated> calendarChannel,
        IAppConfiguration configuration,
        ITimeProvider? timeProvider = null)
    {
        _fetchCalendarEvents = NotNull(fetchCalendarEvents);
        _calendarChannel = NotNull(calendarChannel);
        _pollingInterval = configuration?.PollingInterval ?? TimeSpan.FromMinutes(5);
        _timeProvider = timeProvider ?? new SystemTimeProvider();

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
            var query = new FetchCalendarEventsQuery(
                StartTime: now,
                EndTime: now.AddDays(7));

            var result = await _fetchCalendarEvents.Fetch(query, cancellationToken);

            if (result.IsSuccess)
            {
                var events = result.Match(e => e, _ => []);
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
        IReadOnlyList<MeetingEvent> events,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        var currentEvents = events.ToDictionary(e => e.Id, e => e);

        // Detect added events (in current but not in last known)
        var addedEvents = currentEvents.Values
            .Where(e => !_lastKnownEvents.ContainsKey(e.Id))
            .ToList();

        // Detect removed event IDs (in last known but not in current)
        var removedEventIds = _lastKnownEvents.Keys
            .Where(id => !currentEvents.ContainsKey(id))
            .ToList();

        // Create and publish the update message
        var update = new CalendarEventsUpdated(
            AllEvents: events,
            AddedEvents: addedEvents.AsReadOnly(),
            RemovedEventIds: removedEventIds.AsReadOnly(),
            OccurredAt: occurredAt);

        await _calendarChannel.WriteAsync(update, cancellationToken);

        // Update last known state
        _lastKnownEvents = currentEvents;
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
