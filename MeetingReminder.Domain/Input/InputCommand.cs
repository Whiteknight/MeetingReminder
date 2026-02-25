namespace MeetingReminder.Domain.Input;

/// <summary>
/// Represents a user command parsed from keyboard input.
/// Pure data — no side effects.
/// </summary>
public abstract record InputCommand
{
    private InputCommand() { }

    public sealed record NavigateUp : InputCommand;
    public sealed record NavigateDown : InputCommand;
    public sealed record Acknowledge : InputCommand;
    public sealed record OpenAndAcknowledge : InputCommand;
    public sealed record Quit : InputCommand;
    public sealed record None : InputCommand;
}
