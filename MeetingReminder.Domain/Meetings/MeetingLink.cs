using static MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Value object representing a meeting link extracted from a calendar event.
/// Immutable record that contains the URL and its detected type.
/// </summary>
public record MeetingLink
{
    /// <summary>
    /// The URL of the meeting link
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// The type of meeting link (Google Meet, Zoom, Teams, or Other)
    /// </summary>
    public MeetingLinkType Type { get; init; }

    public MeetingLink(string url, MeetingLinkType type)
    {
        Url = NotNullOrEmpty(url);
        Type = type;
    }
}
