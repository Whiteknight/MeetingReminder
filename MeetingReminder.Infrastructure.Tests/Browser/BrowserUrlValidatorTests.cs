using AwesomeAssertions;
using MeetingReminder.Infrastructure.Browser;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Browser;

/// <summary>
/// Unit tests for BrowserUrlValidator.
/// Tests that actually open browsers are in MeetingReminder.ManualTests.
/// </summary>
[TestFixture]
public class BrowserUrlValidatorTests
{
    [Test]
    public void IsValid_WithNullUrl_ReturnsFalse()
        => BrowserUrlValidator.IsValid(null!).Should().BeFalse();

    [Test]
    public void IsValid_WithEmptyUrl_ReturnsFalse()
        => BrowserUrlValidator.IsValid(string.Empty).Should().BeFalse();

    [Test]
    public void IsValid_WithWhitespaceUrl_ReturnsFalse()
        => BrowserUrlValidator.IsValid("   ").Should().BeFalse();

    [Test]
    public void IsValid_WithFtpScheme_ReturnsFalse()
        => BrowserUrlValidator.IsValid("ftp://example.com").Should().BeFalse();

    [Test]
    public void IsValid_WithFileScheme_ReturnsFalse()
        => BrowserUrlValidator.IsValid("file:///C:/test.html").Should().BeFalse();

    [Test]
    public void IsValid_WithMalformedUrl_ReturnsFalse()
        => BrowserUrlValidator.IsValid("not-a-valid-url").Should().BeFalse();

    [Test]
    public void IsValid_WithRelativeUrl_ReturnsFalse()
        => BrowserUrlValidator.IsValid("/path/to/page").Should().BeFalse();

    [Test]
    public void IsValid_WithHttpUrl_ReturnsTrue()
        => BrowserUrlValidator.IsValid("http://example.com").Should().BeTrue();

    [Test]
    public void IsValid_WithHttpsUrl_ReturnsTrue()
        => BrowserUrlValidator.IsValid("https://example.com/path?q=1").Should().BeTrue();
}
