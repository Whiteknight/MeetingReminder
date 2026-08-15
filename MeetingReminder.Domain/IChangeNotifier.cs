namespace MeetingReminder.Domain;

// TODO: This interface relies on ConsoleKeyInfo which is specific to the terminal domain and
// won't be relevant or useful for a proper GUI frontend. Consider separating these concerns out
// so that the IChangeNotifier is a pure AutoResetEvent replacement and then a separate channel-based
// solution for nonblocking keyboard input.
public interface IChangeNotifier
{
    /// <summary>
    /// Waits for the next change notification or keyboard input.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <param name="timeout">
    /// Maximum time to wait before returning a default (no-key) value so the caller
    /// can refresh time-sensitive UI such as a countdown display.
    /// Pass <see cref="Timeout.InfiniteTimeSpan"/> or <c>default</c> to wait indefinitely.
    /// </param>
    /// <returns>
    /// The next <see cref="ConsoleKeyInfo"/> if one arrived within the timeout,
    /// or <c>default</c> if the timeout elapsed first.
    /// </returns>
    Task<ConsoleKeyInfo> WaitAsync(CancellationToken cancellationToken, TimeSpan timeout = default);

    void Set();

    void Set(ConsoleKeyInfo key);
}
