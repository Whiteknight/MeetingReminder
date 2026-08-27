using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain.Meetings;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class UrlDefenseDecoderTests
{
    [TestFixture]
    public sealed class UnwrapAllTests : UrlDefenseDecoderTests
    {
        [Test]
        public void WithPlainText_ReturnsUnchanged()
        {
            const string text = "Join our meeting at https://meet.google.com/abc-defg-hij today.";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be(text);
        }

        [Test]
        public void WithNoUrls_ReturnsUnchanged()
        {
            const string text = "Please join us for the weekly standup.";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be(text);
        }
    }

    [TestFixture]
    public sealed class V1DecodingTests : UrlDefenseDecoderTests
    {
        // v1 format: https://urldefense.proofpoint.com/v1/?u=<url-encoded>&k=<key>
        // The 'u' param contains a URL-encoded URL; HTML entities may also be present.

        [Test]
        public void DecodesV1WrappedUrl()
        {
            // u= contains URL-encoded https://meet.google.com/abc-defg-hij
            const string wrapped =
                "https://urldefense.proofpoint.com/v1/?u=https%3A%2F%2Fmeet.google.com%2Fabc-defg-hij&k=abc123";

            var result = UrlDefenseDecoder.UnwrapAll(wrapped);

            result.Should().Be("https://meet.google.com/abc-defg-hij");
        }

        [Test]
        public void DecodesV1UrlEmbeddedInText()
        {
            const string text =
                "Join: https://urldefense.proofpoint.com/v1/?u=https%3A%2F%2Fzoom.us%2Fj%2F123456789&k=key1 Thanks";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be("Join: https://zoom.us/j/123456789 Thanks");
        }
    }

    [TestFixture]
    public sealed class V2DecodingTests : UrlDefenseDecoderTests
    {
        // v2 format: https://urldefense.proofpoint.com/v2/url?u=<custom-encoded>&d=<hash>&c=...
        // Custom encoding: - → %, _ → / (then standard URL decode)

        [Test]
        public void DecodesV2WrappedGoogleMeetUrl()
        {
            // Original: https://meet.google.com/abc-defg-hij
            // URL-encoded: https%3A%2F%2Fmeet.google.com%2Fabc-defg-hij
            // Custom-encoded (% → -, / → _): https-3A-2F-2Fmeet.google.com-2Fabc-defg-hij
            // Note: the literal hyphens in the meeting code remain as-is in the original URL,
            // but after round-trip through custom encoding they become %2D. For this test
            // we use a simple path without hyphens in the path segment.
            const string wrapped =
                "https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fexample.com-2Fmeeting-2F123&d=DwMFAg&c=ignored";

            var result = UrlDefenseDecoder.UnwrapAll(wrapped);

            result.Should().Be("https://example.com/meeting/123");
        }

        [Test]
        public void DecodesV2WrappedZoomUrl()
        {
            // Original: https://zoom.us/j/987654321
            // URL-encoded then custom-encoded
            const string wrapped =
                "https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fzoom.us-2Fj-2F987654321&d=DwMF&c=ignored";

            var result = UrlDefenseDecoder.UnwrapAll(wrapped);

            result.Should().Be("https://zoom.us/j/987654321");
        }

        [Test]
        public void DecodesV2WrappedUrlEmbeddedInText()
        {
            // Original: https://teams.microsoft.com/l/meetup-join/abc123
            // URL-encoded: https%3A%2F%2Fteams.microsoft.com%2Fl%2Fmeetup%2Djoin%2Fabc123
            // Custom-encoded (% → -, / → _): https-3A-2F-2Fteams.microsoft.com-2Fl-2Fmeetup-2Djoin-2Fabc123
            const string text =
                "Click here: https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fteams.microsoft.com-2Fl-2Fmeetup-2Djoin-2Fabc123&d=x&c=y for the meeting.";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be("Click here: https://teams.microsoft.com/l/meetup-join/abc123 for the meeting.");
        }
    }

    [TestFixture]
    public sealed class V3DecodingTests : UrlDefenseDecoderTests
    {
        // v3 format: https://urldefense.com/v3/__<url>__;{base64}!{token}
        // The url segment may contain * (single char placeholder) or **X (run placeholder).
        // Placeholders are resolved from a URL-safe base64-decoded byte sequence.

        [Test]
        public void DecodesV3UrlWithNoTokens()
        {
            // When the URL contains no * placeholders the enc_bytes are irrelevant.
            // Format: v3/__<url>__;!!<base64>!<sig>$
            const string wrapped =
                "https://urldefense.com/v3/__https://meet.google.com/abc-defg-hij__;!!PDiH4ENfjr2_Jw!ignored$";

            var result = UrlDefenseDecoder.UnwrapAll(wrapped);

            result.Should().Be("https://meet.google.com/abc-defg-hij");
        }

        [Test]
        public void DecodesV3UrlWithSingleCharTokens()
        {
            // Original: https://example.com/path?q=hello
            // Encoded with * replacing 'h': https://example.com/path?q=*ello
            // enc_bytes base64 of "h" (ASCII 104) = "aA" (url-safe base64, padded to "aA==")
            // urlsafe base64 of single byte 0x68 ('h') = "aA"
            var hBase64 = Convert.ToBase64String("h"u8.ToArray())
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var wrapped =
                $"https://urldefense.com/v3/__https://example.com/path?q=*ello__;{hBase64}!sig$";

            var result = UrlDefenseDecoder.UnwrapAll(wrapped);

            result.Should().Be("https://example.com/path?q=hello");
        }

        [Test]
        public void DecodesV3UrlEmbeddedInText()
        {
            const string text =
                "Details: https://urldefense.com/v3/__https://zoom.us/j/111222333__;!!abc!sig$ - see you there.";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be("Details: https://zoom.us/j/111222333 - see you there.");
        }
    }

    [TestFixture]
    public sealed class MultipleUrlsTests : UrlDefenseDecoderTests
    {
        [Test]
        public void ReplacesMultipleWrappedUrlsInSameText()
        {
            const string text =
                "Docs: https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fdocs.example.com-2F123&d=x&c=y " +
                "and Meet: https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fmeet.google.com-2Fabc&d=x&c=z";

            var result = UrlDefenseDecoder.UnwrapAll(text);

            result.Should().Be("Docs: https://docs.example.com/123 and Meet: https://meet.google.com/abc");
        }
    }
}

[TestFixture]
public class ExtractMeetingLinkUrlDefenseTests
{
    // These tests verify that ExtractMeetingLink.Extract correctly unwraps URL Defense
    // links before identifying the meeting link type.

    [TestFixture]
    public sealed class V2WrappedLinksTests : ExtractMeetingLinkUrlDefenseTests
    {
        [Test]
        public void WithV2WrappedGoogleMeetInDescription_ReturnsGoogleMeetLink()
        {
            // Original: https://meet.google.com/abc-defg-hij
            // Custom-encoded u param: https-3A-2F-2Fmeet.google.com-2Fabc-2Ddefg-2Dhij
            var query = new ExtractMeetingLinkQuery(
                "Join: https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fmeet.google.com-2Fabc-2Ddefg-2Dhij&d=x&c=y",
                null);

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            var link = result.Match(l => l, _ => null!);
            link.Should().BeOfType<GoogleMeetLink>();
            link.Url.Should().Be("https://meet.google.com/abc-defg-hij");
        }

        [Test]
        public void WithV2WrappedZoomInLocation_ReturnsZoomLink()
        {
            var query = new ExtractMeetingLinkQuery(
                null,
                "https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fzoom.us-2Fj-2F987654321&d=x&c=y");

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }

        [Test]
        public void WithV2WrappedTeamsLink_ReturnsMicrosoftTeamsLink()
        {
            // Original: https://teams.microsoft.com/l/meetup-join/abc123
            // Custom-encoded: https-3A-2F-2Fteams.microsoft.com-2Fl-2Fmeetup-2Djoin-2Fabc123
            var query = new ExtractMeetingLinkQuery(
                "https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fteams.microsoft.com-2Fl-2Fmeetup-2Djoin-2Fabc123&d=x&c=y",
                null);

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<MicrosoftTeamsLink>();
        }
    }

    [TestFixture]
    public sealed class V3WrappedLinksTests : ExtractMeetingLinkUrlDefenseTests
    {
        [Test]
        public void WithV3WrappedGoogleMeet_ReturnsGoogleMeetLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://urldefense.com/v3/__https://meet.google.com/abc-defg-hij__;!!PDiH4ENfjr2_Jw!ignored$",
                null);

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }

        [Test]
        public void WithV3WrappedZoom_ReturnsZoomLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://urldefense.com/v3/__https://zoom.us/j/111222333__;!!abc!sig$",
                null);

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }
    }

    [TestFixture]
    public sealed class PriorityPreservedAfterUnwrapTests : ExtractMeetingLinkUrlDefenseTests
    {
        [Test]
        public void WithWrappedGenericAndUnwrappedVideoLink_PrioritizesVideoLink()
        {
            // The doc link is wrapped, the Meet link is plain. After unwrapping both
            // should be plain URLs and Meet should win by priority.
            var query = new ExtractMeetingLinkQuery(
                "Docs: https://urldefense.proofpoint.com/v2/url?u=https-3A-2F-2Fdocs.example.com-2Fabc&d=x&c=y " +
                "Meet: https://meet.google.com/abc-defg-hij",
                null);

            var result = ExtractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }
    }
}
