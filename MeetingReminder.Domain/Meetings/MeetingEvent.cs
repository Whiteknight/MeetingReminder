namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Core domain entity representing a calendar meeting event.
/// Contains all meeting information and domain logic for time calculations.
/// </summary>
public sealed record MeetingEvent(
    MeetingId Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string Description,
    string Location,
    bool IsAllDay,
    string CalendarSource,
    MeetingLink? Link = null)
{
    public static MeetingEvent Create(
        MeetingId id,
        string title,
        DateTime startTime,
        DateTime endTime,
        string description,
        string location,
        bool isAllDay,
        string calendarSource,
        MeetingLink? link = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);
        ArgumentException.ThrowIfNullOrEmpty(calendarSource);
        if (endTime < startTime)
            throw new ArgumentException("End time must be after start time");
        return new MeetingEvent(id, title, startTime, endTime, description ?? string.Empty, location ?? string.Empty, isAllDay, calendarSource, link);
    }

    /// <summary>
    /// Calculates the time remaining until the meeting starts.
    /// Returns negative TimeSpan if the meeting has already started.
    /// </summary>
    /// <param name="currentTime">The current time to calculate from</param>
    /// <returns>TimeSpan representing time until meeting start</returns>
    public TimeSpan GetTimeUntilStart(DateTime currentTime)
        => StartTime - currentTime;

    /// <summary>
    /// Determines if the meeting is currently in progress.
    /// </summary>
    /// <param name="currentTime">The current time to check against</param>
    /// <returns>True if current time is between start and end time</returns>
    public bool IsInProgress(DateTime currentTime)
        => currentTime >= StartTime && currentTime < EndTime;
}
