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
            ConsoleKey.UpArrow => new InputCommand.NavigateUp(),
            ConsoleKey.DownArrow => new InputCommand.NavigateDown(),
            ConsoleKey.Enter or ConsoleKey.Spacebar => new InputCommand.Acknowledge(),
            ConsoleKey.O when !HasCtrl(keyInfo) => new InputCommand.OpenAndAcknowledge(),
            ConsoleKey.C when !HasCtrl(keyInfo) => new InputCommand.Unacknowledge(),
            ConsoleKey.S when !HasCtrl(keyInfo) => new InputCommand.Silence(),
            ConsoleKey.Q when !HasCtrl(keyInfo) => new InputCommand.Quit(),
            _ => new InputCommand.None()
        };

    private static bool HasCtrl(ConsoleKeyInfo keyInfo)
        => keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
}
