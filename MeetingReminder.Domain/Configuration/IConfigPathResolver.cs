namespace MeetingReminder.Domain.Configuration;

/// <summary>
/// Resolves platform-idiomatic paths for application configuration files.
/// </summary>
public interface IConfigPathResolver
{
    /// <summary>
    /// Gets the directory where configuration files are stored.
    /// </summary>
    string GetConfigDirectory();

    /// <summary>
    /// Gets the full path to the main configuration file.
    /// </summary>
    string GetConfigFilePath();

    /// <summary>
    /// Gets the full path to the configuration template file.
    /// </summary>
    string GetTemplateFilePath();
}
