namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Represents a user command parsed from keyboard input.
/// Pure data — no side effects.
/// </summary>
public abstract record TuiCommand
{
    private TuiCommand() { }

    public sealed record NavigateUp : TuiCommand;
    public sealed record NavigateDown : TuiCommand;
    public sealed record Acknowledge : TuiCommand;
    public sealed record OpenAndAcknowledge : TuiCommand;
    public sealed record Quit : TuiCommand;
    public sealed record None : TuiCommand;
}
