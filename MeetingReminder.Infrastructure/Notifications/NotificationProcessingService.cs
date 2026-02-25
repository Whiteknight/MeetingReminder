using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Meetings;
using Microsoft.Extensions.Logging;
using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Infrastructure.Notifications;

/// <summary>
/// Service that processes notifications for upcoming meetings.
/// Reads calendar updates from a channel, maintains meeting state,
/// calculates notification levels, and executes notification strategies.
/// </summary>
public class NotificationProcessingService : IDisposable
{
    private static readonly TimeSpan _notificationProcessingInterval = TimeSpan.FromSeconds(10);

    private readonly IEnumerable<INotificationStrategy> _enabledStrategies;
    private readonly CalculateNotificationLevel _calculateNotificationLevel;
    private readonly IAppConfiguration _config;
    private readonly ITimeProvider _timeProvider;
    private readonly IMeetingRepository _meetings;
    private readonly ILogger<NotificationProcessingService>? _logger;

    private Timer? _notificationTimer;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the NotificationProcessingService.
    /// </summary>
    /// <param name="strategies">All available notification strategies</param>
    /// <param name="calculateNotificationLevel">Use case for calculating notification levels</param>
    /// <param name="config">Application configuration</param>
    /// <param name="timeProvider">Time provider for testability</param>
    /// <param name="meetingRepository">Optional meeting repository for sharing state with acknowledgement use case</param>
    /// <param name="logger">Optional logger</param>
    public NotificationProcessingService(
        IEnumerable<INotificationStrategy> strategies,
        CalculateNotificationLevel calculateNotificationLevel,
        IAppConfiguration config,
        IMeetingRepository meetingRepository,
        ITimeProvider? timeProvider = null,
        ILogger<NotificationProcessingService>? logger = null)
    {
        _calculateNotificationLevel = NotNull(calculateNotificationLevel);
        _config = NotNull(config);
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _meetings = meetingRepository ?? new InMemoryMeetingRepository();
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
    /// Starts the notification processing service.
    /// Begins reading from the calendar channel and processing notifications.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_notificationTimer is not null)
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start timer to process notifications every 10 seconds
        _notificationTimer = new Timer(
            callback: _ => _ = ProcessNotificationsAsync(),
            state: null,
            dueTime: _notificationProcessingInterval,
            period: _notificationProcessingInterval);

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

    private async Task ProcessNotificationsAsync()
    {
        if (_disposed)
            return;

        var currentTime = _timeProvider.UtcNow;
        var activeNotifications = new List<MeetingState>();

        // TODO: More graceful handling of this error, if any, and logging.
        var meetingStates = _meetings.GetAll();
        if (meetingStates.IsError)
            return;

        foreach (var state in meetingStates.GetValueOrDefault([]))
        {
            if (state.IsAcknowledged)
                continue;

            // Get calendar-specific notification rules
            var rules = GetCalendarNotificationRules(state.Event.CalendarSource);
            var notificationLevel = _calculateNotificationLevel.Calculate(new CalculateNotificationLevelQuery(
                Meeting: state.Event,
                CurrentTime: currentTime,
                Thresholds: _config.Thresholds,
                Rules: rules));

            if (!notificationLevel.IsSuccess)
                continue;
            var newLevel = notificationLevel.Match(level => level, _ => NotificationLevel.None);

            // Track previous level before updating
            var previousLevel = state.CurrentLevel;

            // Update notification level (only escalates, never decreases - Requirement 8.5)
            var levelChanged = state.UpdateNotificationLevel(newLevel);
            if (!levelChanged || newLevel == NotificationLevel.None)
                continue;

            // TODO: Want to group some of these together. Toast messages go individually, but beep/sound alerts need to be combined or scheduled.
            // Execute notification strategies with appropriate methods
            await ExecuteNotificationStrategiesAsync(
                previousLevel: previousLevel,
                currentLevel: state.CurrentLevel,
                levelChanged: levelChanged,
                meeting: state.Event);

            state.UpdateLastNotificationTime(currentTime);
            activeNotifications.Add(state);
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
                    _logger?.LogWarning("Notification strategy {Strategy} cycle execution failed: {Error}", strategy.StrategyName, error?.Message);
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
                _logger?.LogError(ex, "Notification strategy {Strategy} threw an exception", strategy.StrategyName);
            }
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
            _notificationTimer?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        _disposed = true;
    }
}
