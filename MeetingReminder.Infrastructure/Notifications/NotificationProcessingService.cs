using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Meetings;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Infrastructure.Notifications;

/// <summary>
/// Service that processes notifications for upcoming meetings.
/// Reads calendar updates from a channel, maintains meeting state,
/// calculates notification levels, and executes notification strategies.
/// </summary>
public class NotificationProcessingService : IDisposable
{
    private static readonly TimeSpan NotificationProcessingInterval = TimeSpan.FromSeconds(10);

    private readonly ChannelReader<CalendarEventsUpdated> _calendarChannel;
    private readonly ChannelWriter<NotificationStateChanged> _notificationChannel;
    private readonly IEnumerable<INotificationStrategy> _enabledStrategies;
    private readonly CalculateNotificationLevel _calculateNotificationLevel;
    private readonly IAppConfiguration _config;
    private readonly ITimeProvider _timeProvider;
    private readonly InMemoryMeetingRepository? _meetingRepository;
    private readonly ILogger<NotificationProcessingService>? _logger;

    private readonly ConcurrentDictionary<string, MeetingState> _meetingStates = new();
    private Timer? _notificationTimer;
    private CancellationTokenSource? _cts;
    private Task? _channelReaderTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the NotificationProcessingService.
    /// </summary>
    /// <param name="calendarChannel">Channel to read calendar updates from</param>
    /// <param name="notificationChannel">Channel to write notification state changes to</param>
    /// <param name="strategies">All available notification strategies</param>
    /// <param name="calculateNotificationLevel">Use case for calculating notification levels</param>
    /// <param name="config">Application configuration</param>
    /// <param name="timeProvider">Time provider for testability</param>
    /// <param name="meetingRepository">Optional meeting repository for sharing state with acknowledgement use case</param>
    /// <param name="logger">Optional logger</param>
    public NotificationProcessingService(
        ChannelReader<CalendarEventsUpdated> calendarChannel,
        ChannelWriter<NotificationStateChanged> notificationChannel,
        IEnumerable<INotificationStrategy> strategies,
        CalculateNotificationLevel calculateNotificationLevel,
        IAppConfiguration config,
        ITimeProvider? timeProvider = null,
        IMeetingRepository? meetingRepository = null,
        ILogger<NotificationProcessingService>? logger = null)
    {
        _calendarChannel = NotNull(calendarChannel);
        _notificationChannel = NotNull(notificationChannel);
        _calculateNotificationLevel = NotNull(calculateNotificationLevel);
        _config = NotNull(config);
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _meetingRepository = meetingRepository as InMemoryMeetingRepository;
        _logger = logger;

        // Filter to only enabled and supported strategies (Requirements 9.2, 9.3)
        _enabledStrategies = FilterEnabledStrategies(strategies, config.EnabledNotificationStrategies);
    }

    private static IEnumerable<INotificationStrategy> FilterEnabledStrategies(
        IEnumerable<INotificationStrategy> strategies,
        IReadOnlyList<string> enabledNames)
    {
        return strategies
            .Where(s => enabledNames.Contains(s.StrategyName, StringComparer.OrdinalIgnoreCase))
            .Where(s => s.IsSupported)
            .ToList();
    }

    /// <summary>
    /// Indicates whether the service is currently running.
    /// </summary>
    public bool IsRunning => _notificationTimer is not null && !_disposed;

    /// <summary>
    /// Gets the current meeting states for testing purposes.
    /// </summary>
    internal IReadOnlyDictionary<string, MeetingState> MeetingStates => _meetingStates;

    /// <summary>
    /// Starts the notification processing service.
    /// Begins reading from the calendar channel and processing notifications.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_notificationTimer is not null)
            return Task.CompletedTask; // Already running

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start background task to read calendar updates
        _channelReaderTask = Task.Run(
            () => ReadCalendarUpdatesAsync(_cts.Token),
            _cts.Token);

        // Start timer to process notifications every 10 seconds
        _notificationTimer = new Timer(
            callback: _ => _ = ProcessNotificationsAsync(),
            state: null,
            dueTime: NotificationProcessingInterval,
            period: NotificationProcessingInterval);

        _logger?.LogInformation("Notification processing service started");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the notification processing service.
    /// </summary>
    public async Task StopAsync()
    {
        if (_notificationTimer is null)
            return;

        _cts?.Cancel();

        // Stop the timer
        await _notificationTimer.DisposeAsync();
        _notificationTimer = null;

        // Wait for channel reader to complete
        if (_channelReaderTask is not null)
        {
            try
            {
                await _channelReaderTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        _logger?.LogInformation("Notification processing service stopped");
    }

    /// <summary>
    /// Processes notifications immediately (for testing).
    /// </summary>
    internal async Task ProcessNotificationsNowAsync()
    {
        ThrowIfDisposed();
        await ProcessNotificationsAsync();
    }

    private async Task ReadCalendarUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var update in _calendarChannel.ReadAllAsync(cancellationToken))
            {
                OnCalendarUpdated(update);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading calendar updates");
        }
    }

    private void OnCalendarUpdated(CalendarEventsUpdated update)
    {
        // Remove meetings that were deleted from the calendar (Requirement 7.4)
        foreach (var removedId in update.RemovedEventIds)
        {
            if (_meetingStates.TryRemove(removedId, out var removedState))
            {
                _meetingRepository?.RemoveAsync(removedId);
                _logger?.LogDebug("Removed meeting state for {MeetingId}: {Title}",
                    removedId, removedState.Event.Title);
            }
        }

        // Add or update meeting states
        foreach (var meeting in update.AllEvents)
        {
            var newState = _meetingStates.AddOrUpdate(
                meeting.Id,
                // Add new meeting state
                _ => new MeetingState(meeting),
                // Update existing - preserve acknowledgement status but update event data
                (_, existing) =>
                {
                    if (existing.IsAcknowledged)
                        return existing; // Don't update acknowledged meetings

                    // Create new state with updated event but preserve notification level
                    var newState = new MeetingState(meeting);
                    newState.UpdateNotificationLevel(existing.CurrentLevel);
                    newState.UpdateLastNotificationTime(existing.LastNotificationTime);
                    return newState;
                });

            // Sync to repository for acknowledgement use case
            _meetingRepository?.AddOrUpdateAsync(newState);
        }

        _logger?.LogDebug("Calendar updated: {Total} events, {Added} added, {Removed} removed",
            update.AllEvents.Count, update.AddedEvents.Count, update.RemovedEventIds.Count);
    }

    private async Task ProcessNotificationsAsync()
    {
        if (_disposed)
            return;

        var currentTime = _timeProvider.UtcNow;
        var activeNotifications = new List<MeetingState>();

        foreach (var kvp in _meetingStates)
        {
            var state = kvp.Value;

            // Check if meeting was acknowledged via the repository (from AcknowledgeMeeting use case)
            if (_meetingRepository is not null)
            {
                var repoResult = await _meetingRepository.GetByIdAsync(state.Event.Id);
                if (repoResult.IsSuccess)
                {
                    var repoState = repoResult.Match(s => s, _ => null!);
                    if (repoState.IsAcknowledged && !state.IsAcknowledged)
                    {
                        // Sync acknowledgement from repository to local state
                        state.Acknowledge();
                        _logger?.LogDebug("Synced acknowledgement from repository for {MeetingId}", state.Event.Id);
                    }
                }
            }

            // Skip acknowledged meetings (Requirement 3.2)
            if (state.IsAcknowledged)
                continue;

            // Get calendar-specific notification rules
            var rules = GetCalendarNotificationRules(state.Event.CalendarSource);

            var query = new CalculateNotificationLevelQuery(
                Meeting: state.Event,
                CurrentTime: currentTime,
                Thresholds: _config.Thresholds,
                Rules: rules);

            var result = _calculateNotificationLevel.Calculate(query);

            if (!result.IsSuccess)
                continue;

            var newLevel = result.Match(level => level, _ => NotificationLevel.None);

            if (newLevel == NotificationLevel.None)
                continue;

            // Track previous level before updating
            var previousLevel = state.CurrentLevel;

            // Update notification level (only escalates, never decreases - Requirement 8.5)
            var levelChanged = state.UpdateNotificationLevel(newLevel);

            // Execute notification strategies with appropriate methods
            await ExecuteNotificationStrategiesAsync(
                previousLevel: previousLevel,
                currentLevel: state.CurrentLevel,
                levelChanged: levelChanged,
                meeting: state.Event);

            state.UpdateLastNotificationTime(currentTime);
            activeNotifications.Add(state);
        }

        // Write notification state update to channel (Requirement 8.4)
        if (activeNotifications.Count > 0)
        {
            var stateChanged = new NotificationStateChanged(
                ActiveNotifications: activeNotifications.AsReadOnly(),
                OccurredAt: currentTime);

            await _notificationChannel.WriteAsync(stateChanged);

            _logger?.LogDebug("Processed {Count} active notifications", activeNotifications.Count);
        }
    }

    private ICalendarNotificationRules? GetCalendarNotificationRules(string calendarSource)
    {
        return _config.Calendars
            .FirstOrDefault(c => c.Name.Equals(calendarSource, StringComparison.OrdinalIgnoreCase))
            ?.NotificationRules;
    }

    private async Task ExecuteNotificationStrategiesAsync(
        NotificationLevel previousLevel,
        NotificationLevel currentLevel,
        bool levelChanged,
        MeetingEvent meeting)
    {
        foreach (var strategy in _enabledStrategies)
        {
            try
            {
                // Always execute per-cycle notifications (e.g., beeps, sounds)
                var cycleResult = await strategy.ExecuteOnCycleAsync(currentLevel, meeting);
                if (!cycleResult.IsSuccess)
                {
                    var error = cycleResult.Match(_ => (NotificationError?)null, e => e);
                    _logger?.LogWarning("Notification strategy {Strategy} cycle execution failed: {Error}",
                        strategy.StrategyName, error?.Message);
                }

                // Only execute level-change notifications when level actually changed (e.g., toasts)
                if (levelChanged)
                {
                    var levelChangeResult = await strategy.ExecuteOnLevelChangeAsync(previousLevel, currentLevel, meeting);
                    if (!levelChangeResult.IsSuccess)
                    {
                        var error = levelChangeResult.Match(_ => (NotificationError?)null, e => e);
                        _logger?.LogWarning("Notification strategy {Strategy} level change execution failed: {Error}",
                            strategy.StrategyName, error?.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions to ensure other strategies still execute (Requirement 12.3)
                _logger?.LogError(ex, "Notification strategy {Strategy} threw an exception",
                    strategy.StrategyName);
            }
        }
    }

    /// <summary>
    /// Acknowledges a meeting, stopping all notifications for it.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to acknowledge</param>
    /// <returns>True if the meeting was found and acknowledged</returns>
    public bool AcknowledgeMeeting(string meetingId)
    {
        if (_meetingStates.TryGetValue(meetingId, out var state))
        {
            state.Acknowledge();
            _logger?.LogInformation("Meeting acknowledged: {MeetingId}", meetingId);
            return true;
        }

        return false;
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
            _notificationTimer?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        _disposed = true;
    }
}
