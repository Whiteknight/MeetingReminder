using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Windows.Configuration;

/// <summary>
/// Resolves configuration paths to %APPDATA%\nag\ on Windows.
/// </summary>
public sealed class ConfigPathResolver : IConfigPathResolver
{
    private const string AppName = "nag";
    private const string ConfigFileName = "config.yaml";
    private const string TemplateFileName = "config.template.yaml";

    private readonly string _configDirectory;

    public ConfigPathResolver()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configDirectory = Path.Combine(appData, AppName);
    }

    /// <summary>
    /// Creates a ConfigPathResolver with a custom directory. For use in tests only.
    /// </summary>
    public ConfigPathResolver(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    public string GetConfigDirectory() => _configDirectory;

    public string GetConfigFilePath() => Path.Combine(_configDirectory, ConfigFileName);

    public string GetTemplateFilePath() => Path.Combine(_configDirectory, TemplateFileName);
}
