using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Linux.Configuration;

/// <summary>
/// Resolves configuration paths using XDG conventions on Linux.
/// Uses $XDG_CONFIG_HOME/nag/ when set, otherwise ~/.config/nag/.
/// </summary>
public sealed class ConfigPathResolver : IConfigPathResolver
{
    private const string AppName = "nag";
    private const string ConfigFileName = "config.yaml";
    private const string TemplateFileName = "config.template.yaml";

    private readonly string _configDirectory;

    public ConfigPathResolver()
    {
        _configDirectory = ResolveConfigDirectory();
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

    private static string ResolveConfigDirectory()
    {
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
            return Path.Combine(xdgConfig, AppName);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", AppName);
    }
}
