namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Maps raw console key presses to TUI commands.
/// Pure mapping — no side effects.
/// </summary>
public interface IKeyboardInputHandler
{
    /// <summary>
    /// Translates a key press into a command for the event loop.
    /// </summary>
    TuiCommand MapKey(ConsoleKeyInfo keyInfo);
}
