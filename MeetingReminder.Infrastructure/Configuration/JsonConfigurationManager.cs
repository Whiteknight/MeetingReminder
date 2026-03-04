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

    private readonly string _configPath;

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
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
    }

    /// <inheritdoc />
    public string ConfigurationPath => _configPath;

    /// <inheritdoc />
    public Result<IAppConfiguration, ConfigurationError> LoadConfiguration()
    {
        // If file doesn't exist, return default configuration
        if (!File.Exists(_configPath))
            return AppConfiguration.Default;

        try
        {
            var json = File.ReadAllText(_configPath);
            if (string.IsNullOrWhiteSpace(json))
                return AppConfiguration.Default;

            var config = JsonSerializer.Deserialize<AppConfiguration>(json, _jsonOptions);
            if (config is null)
                return AppConfiguration.Default;

            // Validate the loaded configuration
            return config.Validate()
                .Map(c => (IAppConfiguration)c)
                .MapError(errors => ConfigurationError.ValidationFailed(errors, _configPath));
        }
        catch (JsonException ex)
        {
            return ConfigurationError.ParseFailed(ex.Message, _configPath);
        }
        catch (IOException ex)
        {
            return ConfigurationError.ReadFailed(ex.Message, _configPath);
        }
    }

    /// <inheritdoc />
    public Result<Unit, ConfigurationError> SaveConfiguration(IAppConfiguration configuration)
    {
        if (configuration is null)
        {
            return ConfigurationError.NullConfiguration();
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Convert to concrete type if needed
            var concreteConfig = configuration as AppConfiguration
                ?? ConvertToAppConfiguration(configuration);

            var json = JsonSerializer.Serialize(concreteConfig, _jsonOptions);
            File.WriteAllText(_configPath, json);

            return Unit.Value;
        }
        catch (JsonException ex)
        {
            return ConfigurationError.SerializationFailed(ex.Message, _configPath);
        }
        catch (IOException ex)
        {
            return ConfigurationError.WriteFailed(ex.Message, _configPath);
        }
    }

    private static string GetDefaultConfigPath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".meeting-reminder", "config.json");
    }

    private static AppConfiguration ConvertToAppConfiguration(IAppConfiguration config)
    {
        return new AppConfiguration(
            PollingInterval: config.PollingInterval,
            EnabledNotificationStrategies: config.EnabledNotificationStrategies.ToList(),
            Thresholds: ConvertThresholds(config.Thresholds),
            Calendars: config.Calendars.Select(ConvertCalendar).ToList());
    }

    private static NotificationThresholds ConvertThresholds(INotificationThresholds thresholds)
    {
        return new NotificationThresholds(
            GentleMinutes: thresholds.GentleMinutes,
            ModerateMinutes: thresholds.ModerateMinutes,
            UrgentMinutes: thresholds.UrgentMinutes,
            CriticalMinutes: thresholds.CriticalMinutes);
    }

    private static CalendarConfiguration ConvertCalendar(ICalendarConfiguration calendar)
    {
        return new CalendarConfiguration(
            Name: calendar.Name,
            Type: CalendarType.ICal, // Default, actual type would need to be preserved
            SourceUrl: calendar.SourceUrl,
            NotificationRules: ConvertRules(calendar.NotificationRules));
    }

    private static CalendarNotificationRules ConvertRules(ICalendarNotificationRules rules)
    {
        return new CalendarNotificationRules(
            NotificationWindowStart: rules.NotificationWindowStart,
            NotificationWindowEnd: rules.NotificationWindowEnd,
            UrgencyMultiplier: rules.UrgencyMultiplier);
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
        {
            return TimeSpan.Zero;
        }

        // Try parsing as TimeSpan string (e.g., "00:05:00")
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        // Try parsing as total minutes (e.g., "5" for 5 minutes)
        if (double.TryParse(value, out var minutes))
        {
            return TimeSpan.FromMinutes(minutes);
        }

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
        {
            return null;
        }

        return _innerConverter.Read(ref reader, typeof(TimeSpan), options);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            _innerConverter.Write(writer, value.Value, options);
        }
    }
}
