using MeetingReminder.Domain.Calendars;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Hosted service wrapper that manages the lifecycle of the CalendarPollingService.
/// Starts polling when the host starts and stops gracefully when the host shuts down.
/// </summary>
public class CalendarPollingHostedService : IHostedService
{
    private readonly ICalendarPollingService _pollingService;
    private readonly ILogger<CalendarPollingHostedService> _logger;

    public CalendarPollingHostedService(
        ICalendarPollingService pollingService,
        ILogger<CalendarPollingHostedService> logger)
    {
        _pollingService = pollingService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting calendar polling service");
        await _pollingService.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping calendar polling service");
        await _pollingService.StopAsync();
    }
}
