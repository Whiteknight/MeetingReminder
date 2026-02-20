using AwesomeAssertions;
using MeetingReminder.Infrastructure.Browser;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Browser;

/// <summary>
/// Unit tests for SystemBrowserLauncher URL validation.
/// Tests that actually open browsers are in MeetingReminder.ManualTests.
/// </summary>
[TestFixture]
public class SystemBrowserLauncherTests
{
    private SystemBrowserLauncher _browserLauncher = null!;

    [SetUp]
    public void SetUp()
    {
        _browserLauncher = new SystemBrowserLauncher();
    }

    [Test]
    public void OpenUrl_WithNullUrl_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl(null!);

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithEmptyUrl_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl(string.Empty);

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithWhitespaceUrl_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl("   ");

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithInvalidScheme_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl("ftp://example.com");

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithFileScheme_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl("file:///C:/test.html");

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithMalformedUrl_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl("not-a-valid-url");

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }

    [Test]
    public void OpenUrl_WithRelativeUrl_ReturnsError()
    {
        var result = _browserLauncher.OpenUrl("/path/to/page");

        result.IsError.Should().BeTrue();
        result.Match(_ => (object?)null, e => e).Should().BeOfType<BrowserLaunchError>();
    }
}
