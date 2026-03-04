using System.Text.Json;
using System.Text.Json.Serialization;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// JSON file-based implementation of IConfigurationManager.
/// Loads and saves configuration from ~/.meeting-reminder/config.json
/// </summary>
public sealed class JsonConfigurationManager : IConfigurationManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), new TimeSpanConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Creates a new JsonConfigurationManager with the default configuration path.
    /// </summary>
    public JsonConfigurationManager()
        : this(GetDefaultConfigPath())
    {
    }

    /// <summary>
    /// Creates a new JsonConfigurationManager with a custom configuration path.
    /// </summary>
    /// <param name="configPath">Path to the configuration file</param>
    public JsonConfigurationManager(string configPath)
    {
        ConfigurationPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    }

    /// <inheritdoc />
    public string ConfigurationPath { get; }

    /// <inheritdoc />
    public Result<IAppConfiguration, ConfigurationError> LoadConfiguration()
    {
        // If file doesn't exist, return default configuration
        if (!File.Exists(ConfigurationPath))
            return AppConfiguration.Default;

        try
        {
            var json = File.ReadAllText(ConfigurationPath);
            if (string.IsNullOrWhiteSpace(json))
                return AppConfiguration.Default;

            var config = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);
            if (config is null)
                return AppConfiguration.Default;

            // Validate the loaded configuration
            return config.Validate()
                .Map(c => (IAppConfiguration)c)
                .MapError(errors => ConfigurationError.ValidationFailed(errors, ConfigurationPath));
        }
        catch (JsonException ex)
        {
            return ConfigurationError.ParseFailed(ex.Message, ConfigurationPath);
        }
        catch (IOException ex)
        {
            return ConfigurationError.ReadFailed(ex.Message, ConfigurationPath);
        }
    }

    private static string GetDefaultConfigPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".meeting-reminder", "config.json");
    }
}

/// <summary>
/// Custom JSON converter for TimeSpan to handle human-readable format.
/// Supports formats like "00:05:00" (5 minutes) or ISO 8601 duration.
/// </summary>
internal sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
            return TimeSpan.Zero;

        // Try parsing as TimeSpan string (e.g., "00:05:00")
        if (TimeSpan.TryParse(value, out var timeSpan))
            return timeSpan;

        // Try parsing as total minutes (e.g., "5" for 5 minutes)
        if (double.TryParse(value, out var minutes))
            return TimeSpan.FromMinutes(minutes);

        throw new JsonException($"Unable to parse TimeSpan from value: {value}");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        // Write as standard TimeSpan string format
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Custom JSON converter for nullable TimeSpan.
/// </summary>
internal sealed class NullableTimeSpanConverter : JsonConverter<TimeSpan?>
{
    private readonly TimeSpanConverter _innerConverter = new();

    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return _innerConverter.Read(ref reader, typeof(TimeSpan), options);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            _innerConverter.Write(writer, value.Value, options);
    }
}
