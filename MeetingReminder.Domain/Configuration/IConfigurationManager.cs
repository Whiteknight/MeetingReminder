using MeetingReminder.Domain.Calendars;

namespace MeetingReminder.Domain.Configuration;

/// <summary>
/// Abstraction for loading and managing application configuration.
/// Implementations handle the specifics of configuration storage (file, environment, etc.).
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Loads the application configuration.
    /// Returns default configuration if the configuration source is missing.
    /// </summary>
    /// <returns>Result containing the configuration or a ConfigurationError</returns>
    Result<IAppConfiguration, ConfigurationError> LoadConfiguration();

    /// <summary>
    /// Gets the path where configuration is stored.
    /// </summary>
    string ConfigurationPath { get; }
}

/// <summary>
/// Read-only interface for application configuration.
/// Allows the Application layer to depend on configuration without knowing the concrete type.
/// </summary>
public interface IAppConfiguration
{
    /// <summary>
    /// Interval between calendar polling operations.
    /// </summary>
    TimeSpan PollingInterval { get; }

    /// <summary>
    /// List of enabled notification strategy names.
    /// </summary>
    IReadOnlyList<string> EnabledNotificationStrategies { get; }

    /// <summary>
    /// Notification escalation timing thresholds.
    /// </summary>
    INotificationThresholds Thresholds { get; }

    /// <summary>
    /// List of configured calendar sources.
    /// </summary>
    IReadOnlyList<ICalendarConfiguration> Calendars { get; }

    ICalendarNotificationRules? GetCalendarNotificationRules(CalendarName calendarSource)
        => Calendars
            .FirstOrDefault(c => calendarSource.Equals(c.Name, StringComparison.OrdinalIgnoreCase))
            ?.NotificationRules;
}

/// <summary>
/// Read-only interface for notification thresholds.
/// </summary>
public interface INotificationThresholds
{
    TimeSpan GentleMinutes { get; }

    TimeSpan ModerateMinutes { get; }

    TimeSpan UrgentMinutes { get; }

    TimeSpan CriticalMinutes { get; }
}

/// <summary>
/// Read-only interface for calendar configuration.
/// </summary>
public interface ICalendarConfiguration
{
    string Name { get; }

    string? SourceUrl { get; }

    ICalendarNotificationRules NotificationRules { get; }
}

/// <summary>
/// Read-only interface for calendar notification rules.
/// </summary>
public interface ICalendarNotificationRules
{
    TimeSpan? NotificationWindowStart { get; }

    TimeSpan? NotificationWindowEnd { get; }

    int UrgencyMultiplier { get; }

    /// <summary>
    /// Determines if the given time falls within the configured notification window.
    /// </summary>
    bool IsWithinNotificationWindow(DateTime currentTime);
}
