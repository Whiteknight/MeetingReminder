using System.Diagnostics;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Notifications;

namespace MeetingReminder.Infrastructure.Linux.Notifications;

/// <summary>
/// Linux implementation of system notifications using notify-send (libnotify).
/// </summary>
public class NotificationProvider : ISystemNotificationProvider
{
    private readonly bool _notifySendAvailable;

    public NotificationProvider()
    {
        _notifySendAvailable = IsNotifySendAvailable();
    }

    public bool IsSupported => OperatingSystem.IsLinux() && _notifySendAvailable;

    public async Task ShowNotificationAsync(string title, string body, NotificationLevel level)
    {
        if (!IsSupported)
            return;

        var urgency = GetUrgency(level);
        var expireTime = GetExpireTime(level);
        var icon = GetIcon(level);

        var arguments = BuildArguments(title, body, urgency, expireTime, icon);

        var startInfo = new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }

    private static string BuildArguments(string title, string body, string urgency, int expireTime, string icon)
    {
        var escapedTitle = EscapeShellArg(title);
        var escapedBody = EscapeShellArg(body);

        var args = $"--urgency={urgency} --expire-time={expireTime}";

        if (!string.IsNullOrEmpty(icon))
        {
            args += $" --icon={icon}";
        }

        args += $" \"{escapedTitle}\" \"{escapedBody}\"";

        return args;
    }

    private static string GetUrgency(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => "low",
            NotificationLevel.Moderate => "normal",
            NotificationLevel.Urgent or NotificationLevel.Critical => "critical",
            _ => "normal"
        };
    }

    private static int GetExpireTime(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => 5000,
            NotificationLevel.Moderate => 10000,
            NotificationLevel.Urgent => 15000,
            NotificationLevel.Critical => 0,
            _ => 5000
        };
    }

    private static string GetIcon(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => "appointment-soon",
            NotificationLevel.Moderate => "appointment-soon",
            NotificationLevel.Urgent => "dialog-warning",
            NotificationLevel.Critical => "dialog-error",
            _ => "appointment-soon"
        };
    }

    private static string EscapeShellArg(string arg)
    {
        return arg
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
    }

    private static bool IsNotifySendAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "notify-send",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
