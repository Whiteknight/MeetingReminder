using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using System.Text.RegularExpressions;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Query to extract a meeting link from text content (description and location).
/// </summary>
/// <param name="Description">The meeting description text to search</param>
/// <param name="Location">The meeting location text to search</param>
public readonly record struct ExtractMeetingLinkQuery(string? Description, string? Location);

/// <summary>
/// Extracts meeting links from calendar event text.
/// Prioritizes video conferencing links (Google Meet, Zoom, Teams) over generic URLs.
/// Uses source-generated regex for optimal performance.
/// </summary>
public partial class ExtractMeetingLink
{
    /// <summary>
    /// Extracts a meeting link from the provided query.
    /// </summary>
    /// <param name="query">The query containing description and location text</param>
    /// <returns>A Result containing the extracted MeetingLink or an error if none found</returns>
    public Result<MeetingLink, Error> Extract(ExtractMeetingLinkQuery query)
    {
        var searchText = CombineSearchText(query.Description, query.Location);

        if (string.IsNullOrWhiteSpace(searchText))
            return MeetingLinkError.NoLinkFound();

        // Try video conferencing links first (in priority order)
        if (TryExtractGoogleMeet(searchText) is { } googleMeet)
            return googleMeet;

        if (TryExtractZoom(searchText) is { } zoom)
            return zoom;

        if (TryExtractTeams(searchText) is { } teams)
            return teams;

        // Fall back to generic URL
        return ExtractGenericUrl(searchText);
    }

    private static string CombineSearchText(string? description, string? location)
        => $"{description ?? string.Empty} {location ?? string.Empty}";

    private static GoogleMeetLink? TryExtractGoogleMeet(string searchText)
    {
        var match = GoogleMeetRegex().Match(searchText);
        return match.Success ? new GoogleMeetLink(match.Value) : null;
    }

    private static ZoomLink? TryExtractZoom(string searchText)
    {
        var match = ZoomRegex().Match(searchText);
        return match.Success ? new ZoomLink(match.Value) : null;
    }

    private static MicrosoftTeamsLink? TryExtractTeams(string searchText)
    {
        var match = TeamsRegex().Match(searchText);
        return match.Success ? new MicrosoftTeamsLink(match.Value) : null;
    }

    private static Result<MeetingLink, Error> ExtractGenericUrl(string searchText)
    {
        var match = GenericUrlRegex().Match(searchText);
        return match.Success
            ? new OtherLink(CleanUrl(match.Value))
            : MeetingLinkError.NoLinkFound();
    }

    private static string CleanUrl(string url)
        => url.TrimEnd('.', ',', ';', ':', ')', ']', '}');

    // Google Meet URLs: https://meet.google.com/xxx-xxxx-xxx
    [GeneratedRegex(@"https?://meet\.google\.com/[a-z]{3}-[a-z]{4}-[a-z]{3}", RegexOptions.IgnoreCase)]
    private static partial Regex GoogleMeetRegex();

    // Zoom URLs: https://zoom.us/j/123456789 or https://us02web.zoom.us/j/123456789
    [GeneratedRegex(@"https?://(?:[\w-]+\.)?zoom\.us/(?:j|my)/[\w\-?=&]+", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomRegex();

    // Teams URLs: https://teams.microsoft.com/l/meetup-join/...
    [GeneratedRegex(@"https?://teams\.microsoft\.com/l/meetup-join/[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex TeamsRegex();

    // Generic URL pattern for fallback
    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex GenericUrlRegex();
}
