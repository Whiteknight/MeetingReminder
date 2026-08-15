namespace MeetingReminder.Domain.Calendars;

/// <summary>
/// Shared store for the calendar polling schedule.
/// Exposes the next scheduled fetch time so the UI can display a countdown.
/// The backing value is written exclusively by the polling thread and read by the UI thread.
/// </summary>
public interface IPollingSchedule
{
    /// <summary>
    /// UTC time at which the next calendar fetch is expected to begin.
    /// Null until the first fetch has been scheduled.
    /// </summary>
    DateTime? NextFetchAt { get; }
}
