namespace MeetingReminder.Domain;

/// <summary>
/// Abstraction for time operations to enable testing.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTime Now { get; }
}

/// <summary>
/// Default implementation that uses system time.
/// </summary>
public class SystemTimeProvider : ITimeProvider
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}
