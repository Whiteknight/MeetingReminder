//using MeetingReminder.Domain;
//using MeetingReminder.Domain.Meetings;
//using MeetingReminder.Domain.Notifications;

//namespace MeetingReminder.Infrastructure.Notifications;

///// <summary>
///// Notification strategy that plays audio files with escalating intensity based on urgency level.
///// Requires a platform-specific ISoundPlayer to be injected.
///// Sounds execute on every polling cycle for persistent audio reminders.
///// </summary>
//public class SoundFileStrategy : INotificationStrategy
//{
//    private readonly ISoundPlayer _soundPlayer;
//    private readonly SoundFileConfiguration _configuration;

//    public SoundFileStrategy(SoundFileConfiguration configuration, ISoundPlayer soundPlayer)
//    {
//        _configuration = configuration;
//        _soundPlayer = soundPlayer;
//    }

//    public string StrategyName => "SoundFile";

//    public bool IsSupported => _soundPlayer.IsSupported;

//    /// <summary>
//    /// Plays sound on every polling cycle for persistent audio reminders.
//    /// </summary>
//    public async Task<Result<Unit, NotificationError>> ExecuteOnCycleAsync(NotificationLevel level, MeetingEvent meeting)
//    {
//        if (!IsSupported)
//        {
//            return new NotificationError("Sound file playback is not supported on this platform", StrategyName);
//        }

//        if (level == NotificationLevel.None)
//        {
//            return Unit.Value;
//        }

//        var soundFile = GetSoundFileForLevel(level);
//        if (string.IsNullOrEmpty(soundFile) || !File.Exists(soundFile))
//        {
//            return new NotificationError($"Sound file not found for level {level}: {soundFile}", StrategyName);
//        }

//        try
//        {
//            var volume = GetVolumeForLevel(level);
//            await _soundPlayer.PlayAsync(soundFile, volume);
//            return Unit.Value;
//        }
//        catch (Exception ex)
//        {
//            return new NotificationError($"Failed to play sound file: {ex.Message}", StrategyName);
//        }
//    }

//    /// <summary>
//    /// Sounds don't need special handling on level change - they execute every cycle.
//    /// </summary>
//    public Task<Result<Unit, NotificationError>> ExecuteOnLevelChangeAsync(
//        NotificationLevel previousLevel,
//        NotificationLevel newLevel,
//        MeetingEvent meeting)
//    {
//        // Sounds execute on every cycle, not just on level change
//        return Task.FromResult<Result<Unit, NotificationError>>(Unit.Value);
//    }

//    private string? GetSoundFileForLevel(NotificationLevel level)
//    {
//        return level switch
//        {
//            NotificationLevel.Gentle => _configuration.GentleSoundPath,
//            NotificationLevel.Moderate => _configuration.ModerateSoundPath,
//            NotificationLevel.Urgent => _configuration.UrgentSoundPath,
//            NotificationLevel.Critical => _configuration.CriticalSoundPath,
//            _ => null
//        };
//    }

//    private static float GetVolumeForLevel(NotificationLevel level)
//    {
//        return level switch
//        {
//            NotificationLevel.Gentle => 0.3f,
//            NotificationLevel.Moderate => 0.5f,
//            NotificationLevel.Urgent => 0.7f,
//            NotificationLevel.Critical => 1.0f,
//            _ => 0.5f
//        };
//    }
//}

///// <summary>
///// Configuration for sound file paths at different urgency levels.
///// </summary>
//public record SoundFileConfiguration(
//    string? GentleSoundPath,
//    string? ModerateSoundPath,
//    string? UrgentSoundPath,
//    string? CriticalSoundPath)
//{
//    public static SoundFileConfiguration Default => new(null, null, null, null);
//}

///// <summary>
///// Abstraction for platform-specific sound playback.
///// </summary>
//public interface ISoundPlayer
//{
//    bool IsSupported { get; }
//    Task PlayAsync(string filePath, float volume);
//}
