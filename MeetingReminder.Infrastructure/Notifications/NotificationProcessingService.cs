using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
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
        ITimeProvider timeProvider,
        ILogger<NotificationProcessingService>? logger = null)
    {
        _calculateNotificationLevel = NotNull(calculateNotificationLevel);
        _config = NotNull(config);
        _timeProvider = timeProvider;
        _meetings = meetingRepository;
        _logger = logger;

        // Filter to only enabled and supported strategies (Requirements 9.2, 9.3)
        _enabledStrategies = strategies
            .Where(s => config.EnabledNotificationStrategies.Contains(s.StrategyName, StringComparer.OrdinalIgnoreCase))
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
    }

    private async Task ProcessNotificationsAsync()
    {
        if (_disposed)
            return;

        var currentTime = _timeProvider.UtcNow;

        var meetings = _meetings.GetAll().Map(r => r.ToArray()).GetValueOrDefault([]);

        for (int i = 0; i < meetings.Length; i++)
        {
            var state = meetings[i];
            if (state.IsAcknowledged)
                continue;

            // Get calendar-specific notification rules
            var rules = _config.GetCalendarNotificationRules(state.Event.Calendar);
            var newLevel = _calculateNotificationLevel.Calculate(new CalculateNotificationLevelQuery(
                Meeting: state.Event,
                CurrentTime: currentTime,
                Thresholds: _config.Thresholds,
                Rules: rules));

            // Update notification level (only escalates, never decreases - Requirement 8.5)
            meetings[i] = state.UpdateNotificationLevel(newLevel, _timeProvider.UtcNow);
            _meetings.Update(meetings[i]);
        }

        await ExecuteNotificationStrategiesAsync(meetings.Where(s => !s.IsAcknowledged && s.CurrentLevel != NotificationLevel.None).ToList());
    }

    private async Task ExecuteNotificationStrategiesAsync(IReadOnlyList<MeetingState> meetings)
    {
        foreach (var strategy in _enabledStrategies)
        {
            try
            {
                await TryExecuteStrategy(meetings, strategy);
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions to ensure other strategies still execute (Requirement 12.3)
                _logger?.LogError(ex, "Notification strategy {Strategy} threw an exception", strategy.StrategyName);
            }
        }
    }

    private async Task TryExecuteStrategy(IReadOnlyList<MeetingState> meetings, INotificationStrategy strategy)
    {
        // Always execute per-cycle notifications (e.g., beeps, sounds)
        var cycleResult = await strategy.ExecuteOnCycleAsync(meetings);
        cycleResult.OnError(error => _logger?.LogWarning("Notification strategy {Strategy} cycle execution failed: {Error}", strategy.StrategyName, error?.Message));

        // Only execute level-change notifications when level actually changed (e.g., toasts)
        foreach (var meeting in meetings.Where(m => m.NotificationLevelHasChanged))
        {
            var levelChangeResult = await strategy.ExecuteOnLevelChangeAsync(meeting);
            levelChangeResult.OnError(error => _logger?.LogWarning("Notification strategy {Strategy} level change execution failed: {Error}", strategy.StrategyName, error?.Message));
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
