using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Core domain entity representing a calendar meeting event.
/// Contains all meeting information and domain logic for time calculations.
/// </summary>
public class MeetingEvent
{
    /// <summary>
    /// Unique identifier for the meeting event
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Title/subject of the meeting
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Meeting start date and time
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Meeting end date and time
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Meeting description/body text
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Meeting location (physical or virtual)
    /// </summary>
    public string Location { get; init; }

    /// <summary>
    /// Indicates if this is an all-day event
    /// </summary>
    public bool IsAllDay { get; init; }

    /// <summary>
    /// Name of the calendar source this event came from
    /// </summary>
    public string CalendarSource { get; init; }

    /// <summary>
    /// Extracted meeting link, if present
    /// </summary>
    public MeetingLink? Link { get; init; }

    public MeetingEvent(
        string id,
        string title,
        DateTime startTime,
        DateTime endTime,
        string description,
        string location,
        bool isAllDay,
        string calendarSource,
        MeetingLink? link = null)
    {
        Id = NotNullOrEmpty(id);
        Title = NotNullOrEmpty(title);
        StartTime = startTime;
        EndTime = endTime;
        Description = description ?? string.Empty;
        Location = location ?? string.Empty;
        IsAllDay = isAllDay;
        CalendarSource = NotNullOrEmpty(calendarSource);
        Link = link;

        if (EndTime < StartTime)
            throw new ArgumentException("End time must be after start time");
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
