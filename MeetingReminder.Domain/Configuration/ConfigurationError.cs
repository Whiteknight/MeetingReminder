namespace MeetingReminder.Domain.Configuration;

public sealed record ConfigurationError : Error
{
    public string? ConfigKey { get; }

    private ConfigurationError(string message, string? configKey = null)
        : base(message)
    {
        ConfigKey = configKey;
    }

    public static ConfigurationError ValidationFailed(IEnumerable<string> errors, string configPath) =>
        new($"Configuration validation failed: {string.Join("; ", errors)}", configPath);

    public static ConfigurationError ParseFailed(string exceptionMessage, string configPath) =>
        new($"Failed to parse configuration file: {exceptionMessage}", configPath);

    public static ConfigurationError ReadFailed(string exceptionMessage, string configPath) =>
        new($"Failed to read configuration file: {exceptionMessage}", configPath);

    public static ConfigurationError NullConfiguration() =>
        new("Configuration cannot be null");

    public static ConfigurationError SerializationFailed(string exceptionMessage, string configPath) =>
        new($"Failed to serialize configuration: {exceptionMessage}", configPath);

    public static ConfigurationError WriteFailed(string exceptionMessage, string configPath) =>
        new($"Failed to write configuration file: {exceptionMessage}", configPath);
}
