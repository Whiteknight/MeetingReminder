namespace MeetingReminder.Domain.Configuration;

public record ConfigurationError(string Message, string? ConfigKey = null) : Error(Message)
{
    public static ConfigurationError ValidationFailed(IEnumerable<string> errors, string configPath)
        => new($"Configuration validation failed: {string.Join("; ", errors)}", configPath);

    public static ConfigurationError ParseFailed(string exceptionMessage, string configPath)
        => new($"Failed to parse configuration file: {exceptionMessage}", configPath);

    public static ConfigurationError ReadFailed(string exceptionMessage, string configPath)
        => new($"Failed to read configuration file: {exceptionMessage}", configPath);

    public static ConfigurationError FirstRun(string message, string configPath)
        => new FirstRunConfigurationError(message, configPath);
}

public sealed record FirstRunConfigurationError(string Message, string ConfigPath) : ConfigurationError(Message, ConfigPath);
