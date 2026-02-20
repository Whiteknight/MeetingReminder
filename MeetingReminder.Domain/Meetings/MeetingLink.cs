using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Base record representing a meeting link extracted from a calendar event.
/// Use the specific subclasses for known video conferencing providers.
/// </summary>
public abstract record MeetingLink
{
    /// <summary>
    /// The URL of the meeting link
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Indicates whether this is a known video conferencing link
    /// </summary>
    public abstract bool IsVideoConferencing { get; }

    protected MeetingLink(string url)
    {
        Url = NotNullOrEmpty(url);
    }
}

/// <summary>
/// Google Meet video conferencing link
/// </summary>
public sealed record GoogleMeetLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => true;
}

/// <summary>
/// Zoom video conferencing link
/// </summary>
public sealed record ZoomLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => true;
}

/// <summary>
/// Microsoft Teams video conferencing link
/// </summary>
public sealed record MicrosoftTeamsLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => true;
}

/// <summary>
/// Generic URL that doesn't match known video conferencing patterns
/// </summary>
public sealed record OtherLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => false;
}
