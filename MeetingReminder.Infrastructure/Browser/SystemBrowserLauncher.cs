using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MeetingReminder.Infrastructure.Browser;

/// <summary>
/// Platform-aware browser launcher that opens URLs in the system's default browser.
/// Supports Windows, Linux, and macOS.
/// </summary>
public class SystemBrowserLauncher : IBrowserLauncher
{
    /// <summary>
    /// Opens the specified URL in the system's default browser.
    /// </summary>
    /// <param name="url">The URL to open</param>
    /// <returns>A Result indicating success or failure with error details</returns>
    public Result<Unit, Error> OpenUrl(string url)
    {
        if (!IsValidUrl(url))
            return BrowserLaunchError.InvalidUrl(url);

        try
        {
            LaunchBrowser(url);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return BrowserLaunchError.LaunchFailed(url, ex.Message);
        }
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static void LaunchBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            LaunchOnWindows(url);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            LaunchOnLinux(url);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            LaunchOnMacOS(url);
            return;
        }

        throw new PlatformNotSupportedException("Browser launching is not supported on this platform");
    }

    private static void LaunchOnWindows(string url)
    {
        // On Windows, use Process.Start with UseShellExecute = true
        // This opens the URL in the default browser
        var startInfo = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private static void LaunchOnLinux(string url)
    {
        // On Linux, use xdg-open which opens the URL in the default browser
        var startInfo = new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = url,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000); // Wait up to 5 seconds
    }

    private static void LaunchOnMacOS(string url)
    {
        // On macOS, use the 'open' command which opens the URL in the default browser
        var startInfo = new ProcessStartInfo
        {
            FileName = "open",
            Arguments = url,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000); // Wait up to 5 seconds
    }
}
