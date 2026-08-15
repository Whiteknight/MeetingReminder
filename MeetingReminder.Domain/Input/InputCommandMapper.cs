namespace MeetingReminder.Domain.Input;

/// <summary>
/// Pure mapper: ConsoleKeyInfo → TuiCommand.
/// Has no dependencies on TUI state, use cases, or application lifetime.
/// </summary>
public static class InputCommandMapExtensions
{
    public static InputCommand MapToInputCommand(this ConsoleKeyInfo keyInfo)
        => keyInfo.Key switch
        {
            ConsoleKey.UpArrow => InputCommand.NavigateUp,
            ConsoleKey.DownArrow => InputCommand.NavigateDown,
            ConsoleKey.Enter or ConsoleKey.Spacebar => InputCommand.Acknowledge,
            ConsoleKey.O when !HasCtrl(keyInfo) => InputCommand.OpenAndAcknowledge,
            ConsoleKey.C when !HasCtrl(keyInfo) => InputCommand.Unacknowledge,
            ConsoleKey.S when !HasCtrl(keyInfo) => InputCommand.Silence,
            ConsoleKey.Q when !HasCtrl(keyInfo) => InputCommand.Quit,
            _ => InputCommand.None
        };

    private static bool HasCtrl(ConsoleKeyInfo keyInfo)
        => keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
}
