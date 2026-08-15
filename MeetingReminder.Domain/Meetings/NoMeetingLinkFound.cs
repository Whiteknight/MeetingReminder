namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Error type for meeting link extraction failures.
/// </summary>
public sealed record NoMeetingLinkFound : Error
{
    public static NoMeetingLinkFound Instance { get; } = new NoMeetingLinkFound("No meeting link found in the provided text");

    private NoMeetingLinkFound(string message) : base(message) { }
}
