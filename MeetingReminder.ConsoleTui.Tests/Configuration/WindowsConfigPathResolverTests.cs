using AwesomeAssertions;
using NUnit.Framework;
using WindowsConfigPathResolver = MeetingReminder.Infrastructure.Windows.Configuration.ConfigPathResolver;

namespace MeetingReminder.ConsoleTui.Tests.Configuration;

/// <summary>
/// Tests the Windows ConfigPathResolver's path resolution against the OS conventions.
/// Lives in ConsoleTui.Tests because that project already carries the Windows platform reference.
/// </summary>
[TestFixture]
public sealed class WindowsConfigPathResolverTests
{
    [Test]
    public void DefaultConstructor_ResolvesToAppDataNagDirectory()
    {
        var resolver = new WindowsConfigPathResolver();
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nag");

        resolver.GetConfigDirectory().Should().Be(expected);
        resolver.GetConfigFilePath().Should().Be(Path.Combine(expected, "config.yaml"));
        resolver.GetTemplateFilePath().Should().Be(Path.Combine(expected, "config.template.yaml"));
    }

    [Test]
    public void CustomDirectoryConstructor_UsesProvidedDirectory()
    {
        var custom = Path.Combine(Path.GetTempPath(), $"NagTest_{Guid.NewGuid():N}");

        var resolver = new WindowsConfigPathResolver(custom);

        resolver.GetConfigDirectory().Should().Be(custom);
        resolver.GetConfigFilePath().Should().Be(Path.Combine(custom, "config.yaml"));
        resolver.GetTemplateFilePath().Should().Be(Path.Combine(custom, "config.template.yaml"));
    }
}
