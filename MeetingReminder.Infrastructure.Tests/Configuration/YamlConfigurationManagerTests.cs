using AwesomeAssertions;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Infrastructure.Configuration;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Configuration;

[TestFixture]
public sealed class YamlConfigurationManagerTests
{
    private string _testDirectory = null!;
    private ConfigPathResolver _pathResolver = null!;

    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NagTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _pathResolver = new ConfigPathResolver(_testDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    [Test]
    public void LoadConfiguration_WhenFileDoesNotExist_PerformsFirstRunSetup()
    {
        // Arrange
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            _ => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Should().BeOfType<FirstRunConfigurationError>();
                error.Message.Should().Contain("Edit config.yaml to add your calendars");
            });

        // Verify files were created
        File.Exists(_pathResolver.GetConfigFilePath()).Should().BeTrue();
        File.Exists(_pathResolver.GetTemplateFilePath()).Should().BeTrue();
    }

    [Test]
    public void LoadConfiguration_WhenFileIsEmpty_ReturnsDefaultConfiguration()
    {
        // Arrange
        File.WriteAllText(_pathResolver.GetConfigFilePath(), string.Empty);
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            config => config.PollingInterval.Should().Be(TimeSpan.FromMinutes(5)),
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public void LoadConfiguration_WhenFileContainsOnlyWhitespace_ReturnsDefaultConfiguration()
    {
        // Arrange
        File.WriteAllText(_pathResolver.GetConfigFilePath(), "   \n\t  ");
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void LoadConfiguration_WhenYamlIsMalformed_ReturnsConfigurationError()
    {
        // Arrange
        File.WriteAllText(_pathResolver.GetConfigFilePath(), "pollingInterval: [invalid: yaml: {{");
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            _ => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("YAML parse error");
                error.ConfigKey.Should().Be(_pathResolver.GetConfigFilePath());
            });
    }

    [Test]
    public void LoadConfiguration_WhenPollingIntervalTooShort_ReturnsValidationError()
    {
        // Arrange
        File.WriteAllText(_pathResolver.GetConfigFilePath(), """
            pollingInterval: "00:00:30"
            enabledNotificationStrategies:
              - Beep
            thresholds:
              gentleMinutes: "00:15:00"
              moderateMinutes: "00:10:00"
              urgentMinutes: "00:05:00"
              criticalMinutes: "00:01:00"
            calendars: []
            """);
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            _ => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("PollingInterval must be at least 1 minute"));
    }

    [Test]
    public void LoadConfiguration_WhenValidYaml_ReturnsLoadedConfiguration()
    {
        // Arrange
        File.WriteAllText(_pathResolver.GetConfigFilePath(), """
            pollingInterval: "00:10:00"
            enabledNotificationStrategies:
              - Beep
              - SystemNotification
            thresholds:
              gentleMinutes: "00:20:00"
              moderateMinutes: "00:10:00"
              urgentMinutes: "00:03:00"
              criticalMinutes: "00:01:00"
            calendars:
              - name: "Work Calendar"
                type: ICal
                sourceUrl: "https://example.com/calendar.ics"
                notificationRules:
                  notificationWindowStart: "09:00:00"
                  notificationWindowEnd: "17:00:00"
                  urgencyMultiplier: 2
            """);
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            config =>
            {
                config.PollingInterval.Should().Be(TimeSpan.FromMinutes(10));
                config.EnabledNotificationStrategies.Should().HaveCount(2);
                config.EnabledNotificationStrategies.Should().Contain("Beep");
                config.EnabledNotificationStrategies.Should().Contain("SystemNotification");
                config.Thresholds.GentleMinutes.Should().Be(TimeSpan.FromMinutes(20));
                config.Thresholds.ModerateMinutes.Should().Be(TimeSpan.FromMinutes(10));
                config.Thresholds.UrgentMinutes.Should().Be(TimeSpan.FromMinutes(3));
                config.Calendars.Should().HaveCount(1);
                config.Calendars[0].Name.Should().Be("Work Calendar");
            },
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public void LoadConfiguration_FirstRunCreatesDefaultConfigYaml()
    {
        // Arrange
        var manager = new YamlConfigurationManager(_pathResolver);

        // Act
        manager.LoadConfiguration(); // triggers first-run

        // Assert - the created config.yaml should be parseable
        var content = File.ReadAllText(_pathResolver.GetConfigFilePath());
        content.Should().NotBeNullOrWhiteSpace();
        content.Should().Contain("pollingInterval");
    }

    [Test]
    public void ConfigPathResolver_OnWindows_UsesAppData()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Ignore("Windows-only test");
            return;
        }

        var resolver = new ConfigPathResolver();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nag");

        resolver.GetConfigDirectory().Should().Be(expected);
        resolver.GetConfigFilePath().Should().Be(Path.Combine(expected, "config.yaml"));
        resolver.GetTemplateFilePath().Should().Be(Path.Combine(expected, "config.template.yaml"));
    }
}
