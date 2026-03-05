using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.Infrastructure.Notifications;

/// <summary>
/// Service that processes notifications for upcoming meetings.
/// Reads calendar updates from a channel, maintains meeting state,
/// calculates notification levels, and executes notification strategies.
/// </summary>
public class NotificationProcessingService : IDisposable
{
    private static readonly TimeSpan _notificationProcessingInterval = TimeSpan.FromSeconds(10);

    private readonly UpdateAllNotificationLevels _updateAllNotificationLevels;
    private readonly NotifyUser _executeNotificationStrategies;
    private readonly ILogger<NotificationProcessingService>? _logger;

    private Timer? _notificationTimer;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the NotificationProcessingService.
    /// </summary>
    /// <param name="strategies">All available notification strategies</param>
    /// <param name="config">Application configuration</param>
    /// <param name="logger">Optional logger</param>
    public NotificationProcessingService(
        IEnumerable<INotificationStrategy> strategies,
        UpdateAllNotificationLevels updateAllNotificationLevels,
        NotifyUser executeNotificationStrategies,
        IAppConfiguration config,
        ILogger<NotificationProcessingService>? logger = null)
    {
        _updateAllNotificationLevels = updateAllNotificationLevels;
        _executeNotificationStrategies = executeNotificationStrategies;
        _logger = logger;
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

        var nearMeetings = _updateAllNotificationLevels.UpdateAndReturnNotifiableMeetings();

        var notifyResult = await _executeNotificationStrategies.Notify(nearMeetings);
        notifyResult.OnError(error => _logger?.LogError("Error processing notifications: {Error}", error));
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
