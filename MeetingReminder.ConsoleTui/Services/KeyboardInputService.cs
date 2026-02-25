using Microsoft.Extensions.Logging;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Pure mapper: ConsoleKeyInfo → TuiCommand.
/// Has no dependencies on TUI state, use cases, or application lifetime.
/// </summary>
public class KeyboardInputService : IKeyboardInputHandler
{
    private readonly ILogger<KeyboardInputService> _logger;

    public KeyboardInputService(ILogger<KeyboardInputService> logger)
    {
        _logger = logger;
    }

    public TuiCommand MapKey(ConsoleKeyInfo keyInfo)
    {
        var command = MapKeyToCommand(keyInfo);

        if (command is TuiCommand.None)
            _logger.LogDebug("Unhandled key: {Key}", keyInfo.Key);

        return command;
    }

    private static TuiCommand MapKeyToCommand(ConsoleKeyInfo keyInfo)
    {
        if (keyInfo.Key == ConsoleKey.UpArrow)
            return new TuiCommand.NavigateUp();

        if (keyInfo.Key == ConsoleKey.DownArrow)
            return new TuiCommand.NavigateDown();

        if (keyInfo.Key == ConsoleKey.Enter)
            return new TuiCommand.Acknowledge();

        if (keyInfo.Key == ConsoleKey.O && !HasCtrl(keyInfo))
            return new TuiCommand.OpenAndAcknowledge();

        if (keyInfo.Key == ConsoleKey.Q && !HasCtrl(keyInfo))
            return new TuiCommand.Quit();

        return new TuiCommand.None();
    }

    // TODO: Make extension method
    private static bool HasCtrl(ConsoleKeyInfo keyInfo)
        => keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control);
}
