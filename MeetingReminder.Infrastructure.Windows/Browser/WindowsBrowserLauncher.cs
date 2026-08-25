using System.Diagnostics;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using MeetingReminder.Infrastructure.Browser;

namespace MeetingReminder.Infrastructure.Windows.Browser;

/// <summary>
/// Windows implementation of IBrowserLauncher.
/// Opens URLs via Process.Start with UseShellExecute, which delegates to the default browser.
/// </summary>
public class WindowsBrowserLauncher : IBrowserLauncher
{
    /// <inheritdoc />
    public Result<Unit, Error> OpenUrl(string url)
    {
        if (!BrowserUrlValidator.IsValid(url))
            return BrowserLaunchError.InvalidUrl(url);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return Unit.Value;
        }
        catch (Exception ex)
        {
            return BrowserLaunchError.LaunchFailed(url, ex.Message);
        }
    }
}
