namespace MeetingReminder.Domain.Browsers;

/// <summary>
/// Abstraction for launching URLs in the system's default browser.
/// Implementations handle platform-specific browser launching.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Opens the specified URL in the system's default browser.
    /// </summary>
    /// <param name="url">The URL to open</param>
    /// <returns>A Result indicating success or failure with error details</returns>
    Result<Unit, Error> OpenUrl(string url);
}
