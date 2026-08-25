using System.Diagnostics;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using MeetingReminder.Infrastructure.Browser;

namespace MeetingReminder.Infrastructure.Linux.Browser;

/// <summary>
/// Linux implementation of IBrowserLauncher.
/// Opens URLs via xdg-open, which delegates to the desktop environment's default browser.
/// </summary>
public class LinuxBrowserLauncher : IBrowserLauncher
{
    /// <inheritdoc />
    public Result<Unit, Error> OpenUrl(string url)
    {
        if (!BrowserUrlValidator.IsValid(url))
            return BrowserLaunchError.InvalidUrl(url);

        try
        {
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
            process?.WaitForExit(5000);

            return Unit.Value;
        }
        catch (Exception ex)
        {
            return BrowserLaunchError.LaunchFailed(url, ex.Message);
        }
    }
}
