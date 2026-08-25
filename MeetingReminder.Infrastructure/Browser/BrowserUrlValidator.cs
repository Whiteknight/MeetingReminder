namespace MeetingReminder.Infrastructure.Browser;

/// <summary>
/// Validates URLs before attempting to open them in a browser.
/// Shared across all platform-specific browser launcher implementations.
/// </summary>
public static class BrowserUrlValidator
{
    /// <summary>
    /// Returns true when the URL is absolute and uses http or https.
    /// </summary>
    public static bool IsValid(string url)
        => !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
