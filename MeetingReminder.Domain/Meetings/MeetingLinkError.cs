namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Error type for meeting link extraction failures.
/// </summary>
public sealed record MeetingLinkError : Error
{
    private MeetingLinkError(string message) : base(message) { }

    public static MeetingLinkError NoLinkFound() =>
        new("No meeting link found in the provided text");
}
