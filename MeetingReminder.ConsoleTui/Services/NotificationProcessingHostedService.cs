using MeetingReminder.Infrastructure.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Hosted service wrapper for NotificationProcessingService.
/// Manages the lifecycle of notification processing within the host.
/// </summary>
public class NotificationProcessingHostedService : IHostedService
{
    private readonly NotificationProcessingService _notificationService;
    private readonly ILogger<NotificationProcessingHostedService> _logger;

    public NotificationProcessingHostedService(
        NotificationProcessingService notificationService,
        ILogger<NotificationProcessingHostedService> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting notification processing service");
        await _notificationService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping notification processing service");
        await _notificationService.StopAsync();
    }
}
