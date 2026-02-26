using System.Runtime.InteropServices;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Infrastructure.Windows.Notifications;

/// <summary>
/// Notification strategy that flashes the terminal window on Windows using FlashWindowEx.
/// Supports both legacy console (conhost) and Windows Terminal.
/// Flashes only on level changes to avoid excessive visual distraction.
/// </summary>
public class TerminalFlashStrategy : INotificationStrategy
{
    public string StrategyName => "TerminalFlash";

    public bool IsSupported => OperatingSystem.IsWindows();

    /// <summary>
    /// Terminal flash doesn't execute on every cycle to avoid excessive visual distraction.
    /// </summary>
    public Task<Result<NotificationLevel, NotificationError>> ExecuteOnCycleAsync(IReadOnlyList<MeetingState> meetings)
        => Task.FromResult<Result<NotificationLevel, NotificationError>>(NotificationLevel.None);

    /// <summary>
    /// Flashes the terminal window when the notification level escalates.
    /// </summary>
    public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(MeetingState meeting)
        => Task.FromResult(Execute(meeting.CurrentLevel));

    private Result<Unit, NotificationError> Execute(NotificationLevel level)
    {
        if (!IsSupported)
            return new NotificationError("Terminal flash is not supported on this platform", StrategyName);

        if (level == NotificationLevel.None)
            return Unit.Value;

        try
        {
            FlashWindow(level);
            return Unit.Value;
        }
        catch (Exception ex)
        {
            return new NotificationError($"Failed to flash terminal window: {ex.Message}", StrategyName);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;

    private static void FlashWindow(NotificationLevel level)
    {
        var hwnd = GetTerminalWindow();
        if (hwnd == IntPtr.Zero)
            return;

        var flashCount = GetFlashCount(level);

        var flashInfo = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = level == NotificationLevel.Critical ? FLASHW_ALL : FLASHW_ALL,
            uCount = flashCount,
            dwTimeout = 0
        };

        FlashWindowEx(ref flashInfo);
    }

    private static IntPtr GetTerminalWindow()
    {
        // First try the legacy console window
        var consoleWindow = GetConsoleWindow();
        if (consoleWindow != IntPtr.Zero)
        {
            // Check if this is actually visible (conhost might be hidden when using Windows Terminal)
            var className = new System.Text.StringBuilder(256);
            GetClassName(consoleWindow, className, className.Capacity);

            // If we're in conhost, use it
            if (className.ToString() == "ConsoleWindowClass")
                return consoleWindow;
        }

        // For Windows Terminal, use the foreground window
        // This works because the test prompts for a keypress, so the terminal should be focused
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var className = new System.Text.StringBuilder(256);
            GetClassName(foregroundWindow, className, className.Capacity);
            var classNameStr = className.ToString();

            // Windows Terminal uses CASCADIA_HOSTING_WINDOW_CLASS
            // Also accept other terminal emulators
            if (classNameStr.Contains("CASCADIA") ||
                classNameStr.Contains("Terminal") ||
                classNameStr.Contains("Console"))
            {
                return foregroundWindow;
            }

            // If we can't identify it but it's the foreground, try it anyway
            return foregroundWindow;
        }

        return consoleWindow;
    }

    private static uint GetFlashCount(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => 2,
            NotificationLevel.Moderate => 4,
            NotificationLevel.Urgent => 6,
            NotificationLevel.Critical => 6,
            _ => 2
        };
    }
}
