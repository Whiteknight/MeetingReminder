using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Browser;

/// <summary>
/// Error type for browser launch failures.
/// </summary>
public sealed record BrowserLaunchError : Error
{
    public string? Url { get; }

    private BrowserLaunchError(string message, string? url = null) : base(message)
    {
        Url = url;
    }

    public static BrowserLaunchError InvalidUrl(string url) =>
        new($"Invalid URL format: {url}", url);

    public static BrowserLaunchError LaunchFailed(string url, string reason) =>
        new($"Failed to launch browser for URL '{url}': {reason}", url);

    public static BrowserLaunchError UnsupportedPlatform() =>
        new("Browser launching is not supported on this platform");
}
