//using AwesomeAssertions;
//using MeetingReminder.Domain.Meetings;
//using MeetingReminder.Domain.Notifications;
//using MeetingReminder.Infrastructure.Notifications;
//using NSubstitute;
//using NUnit.Framework;

//namespace MeetingReminder.Infrastructure.Tests.Notifications;

//[TestFixture]
//public class SoundFileStrategyTests
//{
//    private ISoundPlayer _mockSoundPlayer = null!;
//    private SoundFileConfiguration _configuration = null!;
//    private SoundFileStrategy _strategy = null!;

//    [SetUp]
//    public void SetUp()
//    {
//        _mockSoundPlayer = Substitute.For<ISoundPlayer>();
//        _mockSoundPlayer.IsSupported.Returns(true);

//        _configuration = new SoundFileConfiguration(
//            GentleSoundPath: "gentle.wav",
//            ModerateSoundPath: "moderate.wav",
//            UrgentSoundPath: "urgent.wav",
//            CriticalSoundPath: "critical.wav");

//        _strategy = new SoundFileStrategy(_configuration, _mockSoundPlayer);
//    }

//    [Test]
//    public void StrategyName_ReturnsSoundFile()
//    {
//        _strategy.StrategyName.Should().Be("SoundFile");
//    }

//    [Test]
//    public void IsSupported_WhenSoundPlayerSupported_ReturnsTrue()
//    {
//        _mockSoundPlayer.IsSupported.Returns(true);

//        _strategy.IsSupported.Should().BeTrue();
//    }

//    [Test]
//    public void IsSupported_WhenSoundPlayerNotSupported_ReturnsFalse()
//    {
//        _mockSoundPlayer.IsSupported.Returns(false);

//        _strategy.IsSupported.Should().BeFalse();
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WithNoneLevel_ReturnsSuccessWithoutPlaying()
//    {
//        var meeting = CreateTestMeeting();

//        var result = await _strategy.ExecuteOnCycleAsync(NotificationLevel.None, meeting);

//        result.IsSuccess.Should().BeTrue();
//        await _mockSoundPlayer.DidNotReceive().PlayAsync(Arg.Any<string>(), Arg.Any<float>());
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WhenNotSupported_ReturnsError()
//    {
//        _mockSoundPlayer.IsSupported.Returns(false);
//        var meeting = CreateTestMeeting();

//        var result = await _strategy.ExecuteOnCycleAsync(NotificationLevel.Gentle, meeting);

//        result.IsError.Should().BeTrue();
//        result.Match(
//            _ => string.Empty,
//            error => error.Message).Should().Contain("not supported");
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WhenSoundFileNotFound_ReturnsError()
//    {
//        // Use a configuration with non-existent files
//        var configWithMissingFiles = new SoundFileConfiguration(
//            GentleSoundPath: "nonexistent.wav",
//            ModerateSoundPath: null,
//            UrgentSoundPath: null,
//            CriticalSoundPath: null);

//        var strategy = new SoundFileStrategy(configWithMissingFiles, _mockSoundPlayer);
//        var meeting = CreateTestMeeting();

//        var result = await strategy.ExecuteOnCycleAsync(NotificationLevel.Gentle, meeting);

//        result.IsError.Should().BeTrue();
//        result.Match(
//            _ => string.Empty,
//            error => error.Message).Should().Contain("not found");
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WhenSoundPlayerThrows_ReturnsError()
//    {
//        // Create a temp file to pass the file existence check
//        var tempFile = Path.GetTempFileName();
//        try
//        {
//            var configWithTempFile = new SoundFileConfiguration(
//                GentleSoundPath: tempFile,
//                ModerateSoundPath: null,
//                UrgentSoundPath: null,
//                CriticalSoundPath: null);

//            _mockSoundPlayer.PlayAsync(Arg.Any<string>(), Arg.Any<float>())
//                .Returns(Task.FromException(new InvalidOperationException("Audio device not available")));

//            var strategy = new SoundFileStrategy(configWithTempFile, _mockSoundPlayer);
//            var meeting = CreateTestMeeting();

//            var result = await strategy.ExecuteOnCycleAsync(NotificationLevel.Gentle, meeting);

//            result.IsError.Should().BeTrue();
//            result.Match(
//                _ => string.Empty,
//                error => error.Message).Should().Contain("Failed to play");
//        }
//        finally
//        {
//            File.Delete(tempFile);
//        }
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WithGentleLevel_PlaysWithLowVolume()
//    {
//        var tempFile = Path.GetTempFileName();
//        try
//        {
//            var config = new SoundFileConfiguration(
//                GentleSoundPath: tempFile,
//                ModerateSoundPath: null,
//                UrgentSoundPath: null,
//                CriticalSoundPath: null);

//            var strategy = new SoundFileStrategy(config, _mockSoundPlayer);
//            var meeting = CreateTestMeeting();

//            await strategy.ExecuteOnCycleAsync(NotificationLevel.Gentle, meeting);

//            await _mockSoundPlayer.Received(1).PlayAsync(tempFile, 0.3f);
//        }
//        finally
//        {
//            File.Delete(tempFile);
//        }
//    }

//    [Test]
//    public async Task ExecuteOnCycleAsync_WithCriticalLevel_PlaysWithFullVolume()
//    {
//        var tempFile = Path.GetTempFileName();
//        try
//        {
//            var config = new SoundFileConfiguration(
//                GentleSoundPath: null,
//                ModerateSoundPath: null,
//                UrgentSoundPath: null,
//                CriticalSoundPath: tempFile);

//            var strategy = new SoundFileStrategy(config, _mockSoundPlayer);
//            var meeting = CreateTestMeeting();

//            await strategy.ExecuteOnCycleAsync(NotificationLevel.Critical, meeting);

//            await _mockSoundPlayer.Received(1).PlayAsync(tempFile, 1.0f);
//        }
//        finally
//        {
//            File.Delete(tempFile);
//        }
//    }

//    [Test]
//    public async Task ExecuteOnLevelChangeAsync_ReturnsSuccessWithoutPlaying()
//    {
//        // SoundFileStrategy doesn't execute on level change, only on cycle
//        var meeting = CreateTestMeeting();

//        var result = await _strategy.ExecuteOnLevelChangeAsync(
//            NotificationLevel.None, NotificationLevel.Gentle, meeting);

//        result.IsSuccess.Should().BeTrue();
//        await _mockSoundPlayer.DidNotReceive().PlayAsync(Arg.Any<string>(), Arg.Any<float>());
//    }

//    private static MeetingEvent CreateTestMeeting() =>
//        new(
//            id: Guid.NewGuid().ToString(),
//            title: "Test Meeting",
//            startTime: DateTime.UtcNow.AddMinutes(5),
//            endTime: DateTime.UtcNow.AddMinutes(65),
//            description: "Test description",
//            location: "Test location",
//            isAllDay: false,
//            calendarSource: "test-calendar");
//}
