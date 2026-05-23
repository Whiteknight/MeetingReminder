using System.Reflection;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MeetingReminder.Infrastructure.Configuration;

/// <summary>
/// YAML file-based implementation of IConfigurationManager.
/// Uses YamlDotNet for deserialization.
/// </summary>
public sealed class YamlConfigurationManager : IConfigurationManager
{
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly IConfigPathResolver _pathResolver;

    public YamlConfigurationManager(IConfigPathResolver pathResolver)
    {
        _pathResolver = pathResolver;
    }

    public string ConfigurationPath => _pathResolver.GetConfigFilePath();

    /// <summary>
    /// Loads configuration from the YAML file.
    /// If the file doesn't exist, performs first-run setup and returns a FirstRun error
    /// signaling the app should exit.
    /// </summary>
    public Result<IAppConfiguration, ConfigurationError> LoadConfiguration()
    {
        var configPath = _pathResolver.GetConfigFilePath();

        if (!File.Exists(configPath))
        {
            PerformFirstRunSetup();
            return ConfigurationError.FirstRun(
                $"Configuration created at {configPath}. Edit config.yaml to add your calendars, then restart.",
                configPath);
        }

        try
        {
            var yaml = File.ReadAllText(configPath);
            if (string.IsNullOrWhiteSpace(yaml))
                return AppConfiguration.Default;

            var config = _deserializer.Deserialize<AppConfiguration>(yaml);
            if (config is null)
                return AppConfiguration.Default;

            return config.Validate()
                .Map(c => (IAppConfiguration)c)
                .MapError(errors => ConfigurationError.ValidationFailed(errors, configPath));
        }
        catch (YamlException ex)
        {
            return ConfigurationError.ParseFailed(
                $"YAML parse error at line {ex.Start.Line}, column {ex.Start.Column}: {ex.InnerException?.Message ?? ex.Message}",
                configPath);
        }
        catch (IOException ex)
        {
            return ConfigurationError.ReadFailed(ex.Message, configPath);
        }
    }

    private void PerformFirstRunSetup()
    {
        var configDir = _pathResolver.GetConfigDirectory();
        Directory.CreateDirectory(configDir);

        // Write default config.yaml
        var defaultConfig = AppConfiguration.Default;
        var yaml = _serializer.Serialize(defaultConfig);
        File.WriteAllText(_pathResolver.GetConfigFilePath(), yaml);

        // Extract and write config.template.yaml from embedded resource
        var templatePath = _pathResolver.GetTemplateFilePath();
        ExtractTemplateResource(templatePath);
    }

    private static void ExtractTemplateResource(string targetPath)
    {
        var callingAssembly = Assembly.GetEntryAssembly() ?? typeof(YamlConfigurationManager).Assembly;

        var resourceName = callingAssembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("config.template.yaml", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            File.WriteAllText(targetPath, "# See project documentation for configuration options.\n");
            return;
        }

        using var stream = callingAssembly.GetManifestResourceStream(resourceName)!;
        using var fileStream = File.Create(targetPath);
        stream.CopyTo(fileStream);
    }
}
