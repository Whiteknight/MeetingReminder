using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Configuration for a single calendar source.
/// Defines the calendar type, connection details, and notification rules.
/// </summary>
/// <param name="Name">Unique name identifier for this calendar</param>
/// <param name="Type">Type of calendar source (GoogleCalendar or ICal)</param>
/// <param name="SourceUrl">URL for iCal sources (null for Google Calendar)</param>
/// <param name="NotificationRules">Per-calendar notification rules</param>
public record CalendarConfiguration(
    string Name,
    CalendarType Type,
    string? SourceUrl,
    CalendarNotificationRules NotificationRules) : ICalendarConfiguration
{
    /// <summary>
    /// Gets the notification rules as the interface type.
    /// </summary>
    ICalendarNotificationRules ICalendarConfiguration.NotificationRules => NotificationRules;

    /// <summary>
    /// Creates a default Google Calendar configuration.
    /// </summary>
    /// <param name="name">Name for the calendar</param>
    /// <returns>A new CalendarConfiguration for Google Calendar</returns>
    public static CalendarConfiguration CreateGoogleCalendar(string name)
        => new CalendarConfiguration(
            Name: name,
            Type: CalendarType.GoogleCalendar,
            SourceUrl: null,
            NotificationRules: CalendarNotificationRules.Default);

    /// <summary>
    /// Creates an iCal calendar configuration.
    /// </summary>
    /// <param name="name">Name for the calendar</param>
    /// <param name="url">URL to the iCal feed</param>
    /// <returns>A new CalendarConfiguration for iCal</returns>
    public static CalendarConfiguration CreateICal(string name, string url)
        => new CalendarConfiguration(
            Name: name,
            Type: CalendarType.ICal,
            SourceUrl: url,
            NotificationRules: CalendarNotificationRules.Default);

    /// <summary>
    /// Validates that the configuration is properly set up.
    /// </summary>
    /// <returns>True if configuration is valid</returns>
    public bool IsValid()
    {
        // Name is required
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        // iCal requires a source URL
        if (Type == CalendarType.ICal && string.IsNullOrWhiteSpace(SourceUrl))
            return false;

        // Validate notification rules
        if (!NotificationRules.IsValid())
            return false;

        return true;
    }
}
