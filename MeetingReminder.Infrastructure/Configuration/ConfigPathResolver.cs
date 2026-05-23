using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// Resolves configuration paths using platform-idiomatic locations.
/// Windows: %APPDATA%\nag\
/// Linux: ~/.config/nag/ (or $XDG_CONFIG_HOME/nag/)
/// </summary>
public sealed class ConfigPathResolver : IConfigPathResolver
{
    private const string _appName = "nag";
    private const string _configFileName = "config.yaml";
    private const string _templateFileName = "config.template.yaml";

    private readonly string _configDirectory;

    public ConfigPathResolver()
    {
        _configDirectory = ResolveConfigDirectory();
    }

    /// <summary>
    /// Creates a ConfigPathResolver with a custom directory (for testing).
    /// </summary>
    public ConfigPathResolver(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    public string GetConfigDirectory() => _configDirectory;

    public string GetConfigFilePath() => Path.Combine(_configDirectory, _configFileName);

    public string GetTemplateFilePath() => Path.Combine(_configDirectory, _templateFileName);

    private static string ResolveConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, _appName);
        }

        // Linux/other: use XDG_CONFIG_HOME or fallback to ~/.config
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
            return Path.Combine(xdgConfig, _appName);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", _appName);
    }
}
