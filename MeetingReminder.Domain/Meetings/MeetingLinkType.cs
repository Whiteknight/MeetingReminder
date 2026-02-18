namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Represents the type of meeting link detected in a calendar event.
/// </summary>
public enum MeetingLinkType
{
    /// <summary>
    /// Google Meet video conferencing link
    /// </summary>
    GoogleMeet,

    /// <summary>
    /// Zoom video conferencing link
    /// </summary>
    Zoom,

    /// <summary>
    /// Microsoft Teams video conferencing link
    /// </summary>
    MicrosoftTeams,

    /// <summary>
    /// Other URL that doesn't match known video conferencing patterns
    /// </summary>
    Other
}
