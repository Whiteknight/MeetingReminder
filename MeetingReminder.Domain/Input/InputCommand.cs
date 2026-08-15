namespace MeetingReminder.Domain.Input;

/// <summary>
/// Represents a user command parsed from keyboard input.
/// Pure data — no side effects.
/// </summary>
public enum InputCommand
{
    NavigateUp,
    NavigateDown,
    Acknowledge,
    OpenAndAcknowledge,
    Unacknowledge,
    Silence,
    Quit,
    None
}
