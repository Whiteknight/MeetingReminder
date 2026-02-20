using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Root configuration model for the Meeting Reminder application.
/// Contains all configurable settings including polling, notifications, and calendar sources.
/// </summary>
/// <param name="PollingInterval">Interval between calendar polling operations</param>
/// <param name="EnabledNotificationStrategies">List of notification strategy names to enable</param>
/// <param name="Thresholds">Notification escalation timing thresholds</param>
/// <param name="Calendars">List of configured calendar sources</param>
public record AppConfiguration(
    TimeSpan PollingInterval,
    List<string> EnabledNotificationStrategies,
    NotificationThresholds Thresholds,
    List<CalendarConfiguration> Calendars) : IAppConfiguration
{
    /// <summary>
    /// Gets the enabled notification strategies as a read-only list.
    /// </summary>
    IReadOnlyList<string> IAppConfiguration.EnabledNotificationStrategies => EnabledNotificationStrategies;

    /// <summary>
    /// Gets the thresholds as the interface type.
    /// </summary>
    INotificationThresholds IAppConfiguration.Thresholds => Thresholds;

    /// <summary>
    /// Gets the calendars as a read-only list of the interface type.
    /// </summary>
    IReadOnlyList<ICalendarConfiguration> IAppConfiguration.Calendars =>
        Calendars.Cast<ICalendarConfiguration>().ToList();
    /// <summary>
    /// Default configuration with sensible defaults:
    /// - 5 minute polling interval
    /// - Beep and SystemNotification strategies enabled
    /// - Standard notification thresholds (10/5/1 minutes)
    /// - No calendars configured (user must add)
    /// </summary>
    public static AppConfiguration Default => new(
        PollingInterval: TimeSpan.FromMinutes(5),
        EnabledNotificationStrategies: ["Beep", "SystemNotification"],
        Thresholds: NotificationThresholds.Default,
        Calendars: []);

    /// <summary>
    /// Validates the entire configuration.
    /// </summary>
    /// <returns>Result containing this configuration if valid, or a list of validation error messages</returns>
    public Result<AppConfiguration, IReadOnlyList<string>> Validate()
    {
        var errors = new List<string>();

        // Polling interval must be at least 1 minute
        if (PollingInterval < TimeSpan.FromMinutes(1))
            errors.Add("PollingInterval must be at least 1 minute");

        // Polling interval should not exceed 1 hour (reasonable upper bound)
        if (PollingInterval > TimeSpan.FromHours(1))
            errors.Add("PollingInterval should not exceed 1 hour");

        // Validate notification thresholds
        if (!Thresholds.IsValid())
            errors.Add("NotificationThresholds must be in descending order: Gentle > Moderate > Urgent > 0");

        // Validate each calendar configuration
        for (var i = 0; i < Calendars.Count; i++)
        {
            var calendar = Calendars[i];
            if (!calendar.IsValid())
                errors.Add($"Calendar configuration at index {i} ('{calendar.Name}') is invalid");
        }

        // Check for duplicate calendar names
        var duplicateNames = Calendars
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateNames.Count > 0)
            errors.Add($"Duplicate calendar names found: {string.Join(", ", duplicateNames)}");

        if (errors.Count > 0)
            return Result.FromError<AppConfiguration, IReadOnlyList<string>>(errors);

        return this;
    }

    /// <summary>
    /// Checks if the configuration is valid.
    /// </summary>
    /// <returns>True if configuration passes all validation rules</returns>
    public bool IsValid() => Validate().IsSuccess;
}
