namespace MeetingReminder.Domain.Calendars;

/// <summary>
/// Abstraction for the calendar polling service.
/// Implementations poll calendar sources at configured intervals and publish updates.
/// </summary>
public interface ICalendarPollingService : IDisposable
{
    /// <summary>
    /// Starts the polling service.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the polling operation</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the polling service gracefully.
    /// </summary>
    Task StopAsync();
}
