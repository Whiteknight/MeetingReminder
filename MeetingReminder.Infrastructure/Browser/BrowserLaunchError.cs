using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Browser;

/// <summary>
/// Error type for browser launch failures.
/// </summary>
public sealed record BrowserLaunchError(string Message, string Url) : Error(Message)
{
    public static BrowserLaunchError InvalidUrl(string url)
        => new($"Invalid URL format: {url}", url);

    public static BrowserLaunchError LaunchFailed(string url, string reason)
        => new($"Failed to launch browser for URL '{url}': {reason}", url);

    public static BrowserLaunchError UnsupportedPlatform(string url)
        => new("Browser launching is not supported on this platform", url);
}
