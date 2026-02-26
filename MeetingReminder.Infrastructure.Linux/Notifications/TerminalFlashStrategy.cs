using System.Diagnostics;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Infrastructure.Linux.Notifications;

/// <summary>
/// Notification strategy that uses notify-send on Linux to attract attention.
/// True window flashing requires X11/Wayland specific code.
/// Notifications only appear on level changes to avoid notification spam.
/// </summary>
public class TerminalFlashStrategy : INotificationStrategy
{
    public string StrategyName => "TerminalFlash";

    public bool IsSupported => OperatingSystem.IsLinux();

    /// <summary>
    /// Terminal flash doesn't execute on every cycle to avoid notification spam.
    /// </summary>
    public Task<Result<NotificationLevel, NotificationError>> ExecuteOnCycleAsync(IReadOnlyList<MeetingState> meetings)
    {
        // Terminal flash only happens on level change, not every cycle
        return Task.FromResult<Result<NotificationLevel, NotificationError>>(NotificationLevel.None);
    }

    /// <summary>
    /// Shows a notification when the notification level escalates.
    /// </summary>
    public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(MeetingState meeting)
    {
        return Task.FromResult(Execute(meeting));
    }

    private Result<Unit, NotificationError> Execute(MeetingState meeting)
    {
        if (!IsSupported)
            return new NotificationError("Terminal flash is not supported on this platform", StrategyName);

        var level = meeting.CurrentLevel;
        if (level == NotificationLevel.None)
        {
            return Unit.Value;
        }

        try
        {
            FlashWindow(level, meeting);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to flash terminal window: {ex.Message}", StrategyName);
        }
    }

    private static void FlashWindow(NotificationLevel level, MeetingState meeting)
    {
        var urgency = GetLinuxUrgency(level);
        var title = $"Meeting Reminder: {meeting.Event.Title}";
        var body = $"Starting at {meeting.Event.StartTime:HH:mm}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "notify-send",
            Arguments = $"--urgency={urgency} \"{EscapeShellArg(title)}\" \"{EscapeShellArg(body)}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);
    }

    private static string GetLinuxUrgency(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => "low",
            NotificationLevel.Moderate => "normal",
            NotificationLevel.Urgent or NotificationLevel.Critical => "critical",
            _ => "normal"
        };
    }

    private static string EscapeShellArg(string arg)
    {
        return arg.Replace("\"", "\\\"").Replace("$", "\\$").Replace("`", "\\`");
    }
}
