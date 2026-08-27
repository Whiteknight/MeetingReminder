using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Decodes URLs that have been rewritten by Proofpoint URL Defense (urldefense.com / urldefense.proofpoint.com).
/// Supports v1, v2, and v3 rewritten URL formats.
/// </summary>
/// <remarks>
/// URL Defense wraps outbound links in email to proxy them through a threat-scanning service.
/// This decoder reverses that wrapping to recover the original URL so that meeting link
/// extraction can identify the real destination.
///
/// Format references:
///   v1: https://urldefense.proofpoint.com/v1/?u={url-encoded}&amp;k=...
///   v2: https://urldefense.proofpoint.com/v2/url?u={custom-encoded}&amp;[dc]=...
///   v3: https://urldefense.com/v3/__{url}__;{base64}!{token}...
/// </remarks>
public static partial class UrlDefenseDecoder
{
    // Matches the outer urldefense wrapper and captures the version segment.
    [GeneratedRegex(@"https://urldefense(?:\.proofpoint)?\.com/(v[0-9])/", RegexOptions.IgnoreCase)]
    private static partial Regex VersionPattern();

    // v1: u=<url-encoded-url>&k=
    [GeneratedRegex(@"[?&]u=(?<url>.+?)&k=", RegexOptions.IgnoreCase)]
    private static partial Regex V1Pattern();

    // v2: u=<custom-encoded-url>&[dc]=
    [GeneratedRegex(@"[?&]u=(?<url>.+?)&[dc]=", RegexOptions.IgnoreCase)]
    private static partial Regex V2Pattern();

    // v3: v3/__<url>__;{enc_bytes}!{token}
    // Non-greedy .+? is required: _ is valid in URLs, so we can't use a negative char class.
    [GeneratedRegex(@"v3/__(?<url>.+?)__;(?<enc>[^!]*)!", RegexOptions.IgnoreCase)]
    private static partial Regex V3Pattern();

    // v3 encoded tokens: * (single char) or **X (run of chars)
    [GeneratedRegex(@"\*(\*.)?")]
    private static partial Regex V3TokenPattern();

    // v3 single-slash protocol fix: e.g. "https:/foo" → "https://foo"
    [GeneratedRegex(@"^([a-z0-9+.\-]+:/)([^/].+)", RegexOptions.IgnoreCase)]
    private static partial Regex V3SingleSlashPattern();

    // Maps base64 alphabet chars to their run lengths (A=2, B=3, ... z=63, 0=64, ..., _=65)
    private static readonly IReadOnlyDictionary<char, int> RunMapping = BuildRunMapping();

    private static IReadOnlyDictionary<char, int> BuildRunMapping()
    {
        const string alphabet =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        var map = new Dictionary<char, int>(alphabet.Length);
        for (int i = 0; i < alphabet.Length; i++)
            map[alphabet[i]] = i + 2;

        return map;
    }

    /// <summary>
    /// Replaces all URL Defense-wrapped links in the given text with their decoded originals.
    /// Text that contains no URL Defense links is returned unchanged.
    /// </summary>
    public static string UnwrapAll(string text)
    {
        // Find every urldefense URL in the text and replace each one.
        // We walk right-to-left by index so replacements don't shift subsequent match positions.
        var matches = VersionPattern().Matches(text);
        if (matches.Count == 0)
            return text;

        var builder = new StringBuilder(text);
        // Collect (start, length, decoded) tuples to apply in reverse order.
        var replacements = new List<(int Start, int Length, string Decoded)>(matches.Count);

        foreach (Match versionMatch in matches)
        {
            var (start, length, decoded) = TryExtractDefendedUrl(text, versionMatch);
            if (start >= 0)
                replacements.Add((start, length, decoded));
        }

        // Apply in reverse so indices remain valid.
        replacements.Sort(static (a, b) => b.Start.CompareTo(a.Start));
        foreach (var (start, length, decoded) in replacements)
            builder.Replace(text[start..(start + length)], decoded, start, length);

        return builder.ToString();
    }

    private static (int Start, int Length, string Decoded) TryExtractDefendedUrl(
        string text, Match versionMatch)
    {
        // Find the full URL starting from the version match start.
        // A URL Defense URL ends at whitespace or common terminating punctuation.
        int urlStart = versionMatch.Index;
        int urlEnd = FindUrlEnd(text, urlStart);
        string defendedUrl = text[urlStart..urlEnd];

        var decoded = versionMatch.Groups[1].Value switch
        {
            "v1" => TryDecodeV1(defendedUrl),
            "v2" => TryDecodeV2(defendedUrl),
            "v3" => TryDecodeV3(defendedUrl),
            _ => null
        };

        if (decoded is null)
            return (-1, 0, string.Empty);

        return (urlStart, urlEnd - urlStart, decoded);
    }

    private static int FindUrlEnd(string text, int start)
    {
        // URL ends at whitespace, HTML tag boundary, or common prose punctuation
        // that would not appear inside a URL.
        int i = start;
        while (i < text.Length && !IsUrlTerminator(text[i]))
            i++;
        return i;
    }

    private static bool IsUrlTerminator(char c)
        => c is ' ' or '\t' or '\n' or '\r' or '<' or '>' or '"' or '\'';

    private static string? TryDecodeV1(string url)
    {
        var match = V1Pattern().Match(url);
        if (!match.Success)
            return null;

        var urlEncoded = match.Groups["url"].Value;
        return HttpUtility.HtmlDecode(Uri.UnescapeDataString(urlEncoded));
    }

    private static string? TryDecodeV2(string url)
    {
        var match = V2Pattern().Match(url);
        if (!match.Success)
            return null;

        // Custom encoding: - → %, _ → /
        var specialEncoded = match.Groups["url"].Value;
        var urlEncoded = specialEncoded.Replace('-', '%').Replace('_', '/');
        return HttpUtility.HtmlDecode(Uri.UnescapeDataString(urlEncoded));
    }

    private static string? TryDecodeV3(string url)
    {
        var match = V3Pattern().Match(url);
        if (!match.Success)
            return null;

        var encodedUrl = Uri.UnescapeDataString(match.Groups["url"].Value);
        encodedUrl = FixSingleSlash(encodedUrl);

        var encBytesRaw = match.Groups["enc"].Value;

        // If there are no token placeholders in the URL, we can return it directly.
        if (!encodedUrl.Contains('*'))
            return encodedUrl;

        // Decode the replacement byte sequence.
        var encBytes = encBytesRaw + "==";
        string decodedBytes;

        try
        {
            var bytes = Convert.FromBase64String(
                encBytes.Replace('-', '+').Replace('_', '/'));
            decodedBytes = Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }

        return SubstituteV3Tokens(encodedUrl, decodedBytes);
    }

    private static string FixSingleSlash(string url)
    {
        var match = V3SingleSlashPattern().Match(url);
        if (!match.Success)
            return url;

        return match.Groups[1].Value + "/" + match.Groups[2].Value;
    }

    private static string SubstituteV3Tokens(string encodedUrl, string decodedBytes)
    {
        // Walk the encodedUrl; each * or **X token is replaced by the next
        // char or run of chars from decodedBytes.
        var result = new StringBuilder(encodedUrl.Length);
        int bytePos = 0;
        int textPos = 0;

        var tokenMatches = V3TokenPattern().Matches(encodedUrl);
        foreach (Match token in tokenMatches)
        {
            // Append literal text before this token.
            result.Append(encodedUrl, textPos, token.Index - textPos);

            if (token.Value == "*")
            {
                if (bytePos < decodedBytes.Length)
                    result.Append(decodedBytes[bytePos++]);
            }
            else
            {
                // **X token — X determines run length via the mapping
                char runKey = token.Value[2];
                if (RunMapping.TryGetValue(runKey, out int runLength))
                {
                    int available = Math.Min(runLength, decodedBytes.Length - bytePos);
                    result.Append(decodedBytes, bytePos, available);
                    bytePos += available;
                }
            }

            textPos = token.Index + token.Length;
        }

        // Append any remaining literal text after the last token.
        result.Append(encodedUrl, textPos, encodedUrl.Length - textPos);
        return result.ToString();
    }
}
