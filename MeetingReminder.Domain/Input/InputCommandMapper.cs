namespace MeetingReminder.Domain.Input;

/// <summary>
/// Pure mapper: ConsoleKeyInfo → TuiCommand.
/// Has no dependencies on TUI state, use cases, or application lifetime.
/// </summary>
public class InputCommandMapper
{
    public InputCommand MapKey(ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.UpArrow)
            return new InputCommand.NavigateUp();

        if (keyInfo.Key == ConsoleKey.DownArrow)
            return new InputCommand.NavigateDown();

        if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.Spacebar)
            return new InputCommand.Acknowledge();

        if (keyInfo.Key == ConsoleKey.O && !HasCtrl(keyInfo))
            return new InputCommand.OpenAndAcknowledge();

        if (keyInfo.Key == ConsoleKey.Q && !HasCtrl(keyInfo))
            return new InputCommand.Quit();

        return new InputCommand.None();
    }

    private static bool HasCtrl(ConsoleKeyInfo keyInfo)
        => keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
}
