using System.Text.RegularExpressions;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;

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
public static partial class ExtractMeetingLink
{
    /// <summary>
    /// Extracts a meeting link from the provided query.
    /// </summary>
    /// <param name="query">The query containing description and location text</param>
    /// <returns>A Result containing the extracted MeetingLink or an error if none found</returns>
    public static Result<MeetingLink, Error> Extract(ExtractMeetingLinkQuery query)
    {
        var searchText = CombineSearchText(query.Description, query.Location);

        if (string.IsNullOrWhiteSpace(searchText))
            return NoMeetingLinkFound.Instance;

        return Result.First(
            searchText,
            ExtractGoogleMeet,
            ExtractZoom,
            ExtractTeams,
            ExtractGenericUrl
        );
    }

    private static string CombineSearchText(string? description, string? location)
        => $"{description ?? string.Empty} {location ?? string.Empty}";

    private static Result<MeetingLink, Error> ExtractGoogleMeet(string searchText)
    {
        var match = GoogleMeetRegex().Match(searchText);
        return match.Success
            ? new GoogleMeetLink(match.Value)
            : NoMeetingLinkFound.Instance;
    }

    private static Result<MeetingLink, Error> ExtractZoom(string searchText)
    {
        var match = ZoomRegex().Match(searchText);
        return match.Success
            ? new ZoomLink(match.Value)
            : NoMeetingLinkFound.Instance;
    }

    private static Result<MeetingLink, Error> ExtractTeams(string searchText)
    {
        var match = TeamsRegex().Match(searchText);
        return match.Success
            ? new MicrosoftTeamsLink(match.Value)
            : NoMeetingLinkFound.Instance;
    }

    private static Result<MeetingLink, Error> ExtractGenericUrl(string searchText)
    {
        var match = GenericUrlRegex().Match(searchText);
        return match.Success
            ? new OtherLink(CleanUrl(match.Value))
            : NoMeetingLinkFound.Instance;
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
