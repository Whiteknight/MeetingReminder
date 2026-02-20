using AwesomeAssertions;
using MeetingReminder.Infrastructure.Configuration;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Configuration;

/// <summary>
/// Unit tests for JsonConfigurationManager covering:
/// - Default configuration when file is missing
/// - JSON parsing errors
/// - Invalid configuration values
/// </summary>
[TestFixture]
public sealed class JsonConfigurationManagerTests
{
    private string _testDirectory = null!;
    private string _testConfigPath = null!;

    [SetUp]
    public void SetUp()
    {
        // Create a unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MeetingReminderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "config.json");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public void LoadConfiguration_WhenFileDoesNotExist_ReturnsDefaultConfiguration()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.json");
        var manager = new JsonConfigurationManager(nonExistentPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            config =>
            {
                config.PollingInterval.Should().Be(TimeSpan.FromMinutes(5));
                config.EnabledNotificationStrategies.Should().Contain("Beep");
                config.EnabledNotificationStrategies.Should().Contain("SystemNotification");
                config.Thresholds.GentleMinutes.Should().Be(TimeSpan.FromMinutes(10));
                config.Thresholds.ModerateMinutes.Should().Be(TimeSpan.FromMinutes(5));
                config.Thresholds.UrgentMinutes.Should().Be(TimeSpan.FromMinutes(1));
                config.Calendars.Should().BeEmpty();
            },
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public void LoadConfiguration_WhenFileIsEmpty_ReturnsDefaultConfiguration()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, string.Empty);
        var manager = new JsonConfigurationManager(_testConfigPath);

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
        File.WriteAllText(_testConfigPath, "   \n\t  ");
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void LoadConfiguration_WhenJsonIsMalformed_ReturnsConfigurationError()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, "{ invalid json }");
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("Failed to parse configuration file");
                error.ConfigKey.Should().Be(_testConfigPath);
            });
    }

    [Test]
    public void LoadConfiguration_WhenJsonHasUnexpectedToken_ReturnsConfigurationError()
    {
        // Arrange - JSON with trailing comma (invalid)
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:05:00",
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("Failed to parse configuration file"));
    }

    [Test]
    public void LoadConfiguration_WhenJsonHasInvalidTimeSpanFormat_ReturnsConfigurationError()
    {
        // Arrange - Invalid TimeSpan format
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "not-a-timespan"
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("Failed to parse configuration file"));
    }

    [Test]
    public void LoadConfiguration_WhenPollingIntervalTooShort_ReturnsConfigurationError()
    {
        // Arrange - Polling interval less than 1 minute
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:00:30",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:15:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:05:00"
                },
                "calendars": []
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("Configuration validation failed");
                error.Message.Should().Contain("PollingInterval must be at least 1 minute");
            });
    }

    [Test]
    public void LoadConfiguration_WhenPollingIntervalTooLong_ReturnsConfigurationError()
    {
        // Arrange - Polling interval exceeds 1 hour
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "02:00:00",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:15:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:05:00"
                },
                "calendars": []
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("Configuration validation failed");
                error.Message.Should().Contain("PollingInterval should not exceed 1 hour");
            });
    }

    [Test]
    public void LoadConfiguration_WhenThresholdsNotInDescendingOrder_ReturnsConfigurationError()
    {
        // Arrange - Thresholds not in correct order (Gentle should be > Moderate > Urgent)
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:05:00",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:05:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:15:00"
                },
                "calendars": []
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("Configuration validation failed");
                error.Message.Should().Contain("NotificationThresholds must be in descending order");
            });
    }

    [Test]
    public void LoadConfiguration_WhenCalendarHasEmptyName_ReturnsConfigurationError()
    {
        // Arrange - Calendar with empty name
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:05:00",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:15:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:05:00"
                },
                "calendars": [
                    {
                        "name": "",
                        "type": "ICal",
                        "sourceUrl": "https://example.com/calendar.ics",
                        "notificationRules": {
                            "urgencyMultiplier": 1
                        }
                    }
                ]
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("Configuration validation failed"));
    }

    [Test]
    public void LoadConfiguration_WhenICalCalendarMissingSourceUrl_ReturnsConfigurationError()
    {
        // Arrange - iCal calendar without source URL
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:05:00",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:15:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:05:00"
                },
                "calendars": [
                    {
                        "name": "Work Calendar",
                        "type": "ICal",
                        "notificationRules": {
                            "urgencyMultiplier": 1
                        }
                    }
                ]
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("Configuration validation failed"));
    }

    [Test]
    public void LoadConfiguration_WhenDuplicateCalendarNames_ReturnsConfigurationError()
    {
        // Arrange - Duplicate calendar names
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:05:00",
                "enabledNotificationStrategies": ["Beep"],
                "thresholds": {
                    "gentleMinutes": "00:15:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:05:00"
                },
                "calendars": [
                    {
                        "name": "Work",
                        "type": "ICal",
                        "sourceUrl": "https://example.com/work.ics",
                        "notificationRules": {
                            "urgencyMultiplier": 1
                        }
                    },
                    {
                        "name": "Work",
                        "type": "ICal",
                        "sourceUrl": "https://example.com/work2.ics",
                        "notificationRules": {
                            "urgencyMultiplier": 1
                        }
                    }
                ]
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

        // Act
        var result = manager.LoadConfiguration();

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            config => throw new AssertionException("Expected error but got success"),
            error =>
            {
                error.Message.Should().Contain("Configuration validation failed");
                error.Message.Should().Contain("Duplicate calendar names");
            });
    }

    [Test]
    public void LoadConfiguration_WhenValidConfiguration_ReturnsLoadedConfiguration()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, """
            {
                "pollingInterval": "00:10:00",
                "enabledNotificationStrategies": ["Beep", "Flash"],
                "thresholds": {
                    "gentleMinutes": "00:20:00",
                    "moderateMinutes": "00:10:00",
                    "urgentMinutes": "00:03:00"
                },
                "calendars": [
                    {
                        "name": "Work Calendar",
                        "type": "ICal",
                        "sourceUrl": "https://example.com/calendar.ics",
                        "notificationRules": {
                            "notificationWindowStart": "09:00:00",
                            "notificationWindowEnd": "17:00:00",
                            "urgencyMultiplier": 2
                        }
                    }
                ]
            }
            """);
        var manager = new JsonConfigurationManager(_testConfigPath);

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
                config.EnabledNotificationStrategies.Should().Contain("Flash");
                config.Thresholds.GentleMinutes.Should().Be(TimeSpan.FromMinutes(20));
                config.Thresholds.ModerateMinutes.Should().Be(TimeSpan.FromMinutes(10));
                config.Thresholds.UrgentMinutes.Should().Be(TimeSpan.FromMinutes(3));
                config.Calendars.Should().HaveCount(1);
                config.Calendars[0].Name.Should().Be("Work Calendar");
            },
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }
}
