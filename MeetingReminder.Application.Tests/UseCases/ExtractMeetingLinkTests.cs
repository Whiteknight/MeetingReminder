using AwesomeAssertions;
using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain.Meetings;
using NUnit.Framework;

namespace MeetingReminder.Application.Tests.UseCases;

[TestFixture]
public class ExtractMeetingLinkTests
{
    private ExtractMeetingLink _extractMeetingLink = null!;

    [SetUp]
    public void SetUp()
    {
        _extractMeetingLink = new ExtractMeetingLink();
    }

    [TestFixture]
    public sealed class NoLinksTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithNullDescriptionAndLocation_ReturnsError()
        {
            var query = new ExtractMeetingLinkQuery(null, null);

            var result = _extractMeetingLink.Extract(query);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public void WithEmptyDescriptionAndLocation_ReturnsError()
        {
            var query = new ExtractMeetingLinkQuery(string.Empty, string.Empty);

            var result = _extractMeetingLink.Extract(query);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public void WithWhitespaceOnly_ReturnsError()
        {
            var query = new ExtractMeetingLinkQuery("   ", "   ");

            var result = _extractMeetingLink.Extract(query);

            result.IsError.Should().BeTrue();
        }

        [Test]
        public void WithTextButNoUrls_ReturnsError()
        {
            var query = new ExtractMeetingLinkQuery(
                "Please join us for the weekly standup meeting",
                "Conference Room A");

            var result = _extractMeetingLink.Extract(query);

            result.IsError.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class GoogleMeetTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithGoogleMeetLink_ReturnsGoogleMeetLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "Join at https://meet.google.com/abc-defg-hij",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            var link = result.Match(l => l, _ => null!);
            link.Should().BeOfType<GoogleMeetLink>();
            link.Url.Should().Be("https://meet.google.com/abc-defg-hij");
            link.IsVideoConferencing.Should().BeTrue();
        }

        [Test]
        public void WithGoogleMeetInLocation_ReturnsGoogleMeetLink()
        {
            var query = new ExtractMeetingLinkQuery(
                null,
                "https://meet.google.com/xyz-abcd-efg");

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }
    }

    [TestFixture]
    public sealed class ZoomTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithZoomLink_ReturnsZoomLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "Join Zoom Meeting: https://zoom.us/j/123456789",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            var link = result.Match(l => l, _ => null!);
            link.Should().BeOfType<ZoomLink>();
            link.Url.Should().Be("https://zoom.us/j/123456789");
            link.IsVideoConferencing.Should().BeTrue();
        }

        [Test]
        public void WithZoomSubdomainLink_ReturnsZoomLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://us02web.zoom.us/j/987654321?pwd=abc123",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }

        [Test]
        public void WithZoomPersonalMeetingRoom_ReturnsZoomLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://zoom.us/my/johndoe",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }
    }

    [TestFixture]
    public sealed class MicrosoftTeamsTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithTeamsLink_ReturnsMicrosoftTeamsLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "Join Teams: https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc123",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            var link = result.Match(l => l, _ => null!);
            link.Should().BeOfType<MicrosoftTeamsLink>();
            link.IsVideoConferencing.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class GenericUrlTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithGenericHttpsUrl_ReturnsOtherLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "Meeting info at https://example.com/meeting/123",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            var link = result.Match(l => l, _ => null!);
            link.Should().BeOfType<OtherLink>();
            link.Url.Should().Be("https://example.com/meeting/123");
            link.IsVideoConferencing.Should().BeFalse();
        }

        [Test]
        public void WithGenericHttpUrl_ReturnsOtherLink()
        {
            var query = new ExtractMeetingLinkQuery(
                "http://intranet.company.com/room/42",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<OtherLink>();
        }
    }

    [TestFixture]
    public sealed class PriorityTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithGoogleMeetAndGenericUrl_PrioritizesGoogleMeet()
        {
            var query = new ExtractMeetingLinkQuery(
                "Agenda: https://docs.google.com/doc/123 Meeting: https://meet.google.com/abc-defg-hij",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }

        [Test]
        public void WithZoomAndGenericUrl_PrioritizesZoom()
        {
            var query = new ExtractMeetingLinkQuery(
                "Notes: https://notion.so/meeting-notes Join: https://zoom.us/j/123456789",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }

        [Test]
        public void WithTeamsAndGenericUrl_PrioritizesTeams()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://sharepoint.com/doc https://teams.microsoft.com/l/meetup-join/abc",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<MicrosoftTeamsLink>();
        }
    }

    [TestFixture]
    public sealed class MultipleVideoLinksTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithMultipleVideoLinks_ReturnsFirstByPriority()
        {
            var query = new ExtractMeetingLinkQuery(
                "Zoom: https://zoom.us/j/123 Meet: https://meet.google.com/abc-defg-hij",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }
    }

    [TestFixture]
    public sealed class MalformedUrlTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithMalformedGoogleMeetUrl_FallsBackToGeneric()
        {
            var query = new ExtractMeetingLinkQuery(
                "https://meet.google.com/invalid",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<OtherLink>();
        }

        [Test]
        public void WithUrlContainingTrailingPunctuation_CleansUrl()
        {
            var query = new ExtractMeetingLinkQuery(
                "Visit https://example.com/meeting.",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l.Url, _ => string.Empty).Should().Be("https://example.com/meeting");
        }

        [Test]
        public void WithUrlInParentheses_CleansUrl()
        {
            var query = new ExtractMeetingLinkQuery(
                "Meeting link (https://example.com/room)",
                null);

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l.Url, _ => string.Empty).Should().Be("https://example.com/room");
        }
    }

    [TestFixture]
    public sealed class CombinedDescriptionAndLocationTests : ExtractMeetingLinkTests
    {
        [Test]
        public void WithLinkInLocationOnly_ExtractsFromLocation()
        {
            var query = new ExtractMeetingLinkQuery(
                "Weekly team sync",
                "https://meet.google.com/abc-defg-hij");

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<GoogleMeetLink>();
        }

        [Test]
        public void WithLinksInBothDescriptionAndLocation_PrioritizesVideoConferencing()
        {
            var query = new ExtractMeetingLinkQuery(
                "Agenda at https://docs.google.com/doc/123",
                "https://zoom.us/j/987654321");

            var result = _extractMeetingLink.Extract(query);

            result.IsSuccess.Should().BeTrue();
            result.Match(l => l, _ => null!).Should().BeOfType<ZoomLink>();
        }
    }
}
