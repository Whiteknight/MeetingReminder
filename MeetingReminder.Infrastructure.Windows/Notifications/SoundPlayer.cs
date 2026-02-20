using MeetingReminder.Infrastructure.Notifications;

namespace MeetingReminder.Infrastructure.Windows.Notifications;

/// <summary>
/// Windows implementation of sound playback.
/// </summary>
public class SoundPlayer : ISoundPlayer
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task PlayAsync(string filePath, float volume)
    {
        if (!IsSupported)
            return Task.CompletedTask;

        // Basic implementation - could be enhanced with NAudio for volume control
        return Task.CompletedTask;
    }
}
