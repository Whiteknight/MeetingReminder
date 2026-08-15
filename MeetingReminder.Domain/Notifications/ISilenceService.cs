namespace MeetingReminder.Domain.Notifications;

/// <summary>
/// Manages a temporary silence period during which notification strategies are suppressed.
/// Notification level calculation continues normally; only strategy execution is muted.
/// </summary>
public interface ISilenceService
{
    /// <summary>
    /// Whether silence is currently active (i.e., SilencedUntil is in the future).
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// The UTC time at which silence will expire, or null if not currently silenced.
    /// </summary>
    DateTime? SilencedUntil { get; }

    /// <summary>
    /// Activates silence until the specified UTC time.
    /// Calling this while already silenced extends or replaces the current silence period.
    /// </summary>
    /// <param name="until">UTC time at which silence expires</param>
    void Activate(DateTime until);

    /// <summary>
    /// Immediately deactivates silence, allowing notifications to resume on the next cycle.
    /// </summary>
    void Deactivate();
}
