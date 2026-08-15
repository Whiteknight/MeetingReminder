using AwesomeAssertions;
using MeetingReminder.Domain;
using MeetingReminder.Infrastructure.Notifications;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Notifications;

[TestFixture]
public class SilenceServiceTests
{
    private ITimeProvider _time = null!;
    private SilenceService _silenceService = null!;

    private static readonly DateTime BaseTime = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _time = Substitute.For<ITimeProvider>();
        _time.UtcNow.Returns(BaseTime);
        _silenceService = new SilenceService(_time);
    }

    [TestFixture]
    public sealed class InitialStateTests : SilenceServiceTests
    {
        [Test]
        public void IsActive_WhenNotActivated_ReturnsFalse()
        {
            _silenceService.IsActive.Should().BeFalse();
        }

        [Test]
        public void SilencedUntil_WhenNotActivated_ReturnsNull()
        {
            _silenceService.SilencedUntil.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class ActivateTests : SilenceServiceTests
    {
        [Test]
        public void AfterActivate_IsActiveReturnsTrue()
        {
            _silenceService.Activate(BaseTime.AddMinutes(5));

            _silenceService.IsActive.Should().BeTrue();
        }

        [Test]
        public void AfterActivate_SilencedUntilReturnsExpiry()
        {
            var until = BaseTime.AddMinutes(5);
            _silenceService.Activate(until);

            _silenceService.SilencedUntil.Should().Be(until);
        }

        [Test]
        public void WhenExpiryIsInPast_IsActiveReturnsFalse()
        {
            _silenceService.Activate(BaseTime.AddMinutes(-1));

            _silenceService.IsActive.Should().BeFalse();
        }

        [Test]
        public void WhenExpiryIsNow_IsActiveReturnsFalse()
        {
            _silenceService.Activate(BaseTime);

            _silenceService.IsActive.Should().BeFalse();
        }

        [Test]
        public void CallingActivateTwice_ReplacesExistingSilence()
        {
            _silenceService.Activate(BaseTime.AddMinutes(5));
            var newExpiry = BaseTime.AddMinutes(10);
            _silenceService.Activate(newExpiry);

            _silenceService.SilencedUntil.Should().Be(newExpiry);
            _silenceService.IsActive.Should().BeTrue();
        }

        [Test]
        public void WhenActivatedWithLocalTime_ConvertsToUtc()
        {
            var localTime = BaseTime.ToLocalTime();
            _silenceService.Activate(localTime);

            _silenceService.SilencedUntil!.Value.Kind.Should().Be(DateTimeKind.Utc);
        }
    }

    [TestFixture]
    public sealed class DeactivateTests : SilenceServiceTests
    {
        [Test]
        public void AfterDeactivate_IsActiveReturnsFalse()
        {
            _silenceService.Activate(BaseTime.AddMinutes(5));

            _silenceService.Deactivate();

            _silenceService.IsActive.Should().BeFalse();
        }

        [Test]
        public void AfterDeactivate_SilencedUntilReturnsNull()
        {
            _silenceService.Activate(BaseTime.AddMinutes(5));

            _silenceService.Deactivate();

            _silenceService.SilencedUntil.Should().BeNull();
        }

        [Test]
        public void DeactivateWhenNotActive_DoesNotThrow()
        {
            _silenceService.Invoking(s => s.Deactivate()).Should().NotThrow();
        }
    }

    [TestFixture]
    public sealed class ExpiryTests : SilenceServiceTests
    {
        [Test]
        public void WhenTimeAdvancesPastExpiry_IsActiveReturnsFalse()
        {
            _silenceService.Activate(BaseTime.AddMinutes(5));
            _silenceService.IsActive.Should().BeTrue();

            // Advance time past the expiry
            _time.UtcNow.Returns(BaseTime.AddMinutes(6));

            _silenceService.IsActive.Should().BeFalse();
        }

        [Test]
        public void SilencedUntilRemainsSetAfterExpiry()
        {
            var until = BaseTime.AddMinutes(5);
            _silenceService.Activate(until);

            _time.UtcNow.Returns(BaseTime.AddMinutes(6));

            // SilencedUntil still holds the timestamp; IsActive is what gates behaviour
            _silenceService.SilencedUntil.Should().Be(until);
            _silenceService.IsActive.Should().BeFalse();
        }
    }
}
