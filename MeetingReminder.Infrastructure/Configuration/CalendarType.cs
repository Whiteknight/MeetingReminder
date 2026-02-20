namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Represents the type of calendar source.
/// </summary>
public enum CalendarType
{
    /// <summary>
    /// Google Calendar API integration
    /// </summary>
    GoogleCalendar,

    /// <summary>
    /// iCal/ICS file or URL subscription
    /// </summary>
    ICal
}
