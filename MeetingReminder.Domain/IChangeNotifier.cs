namespace MeetingReminder.Domain;

// TODO: This interface relies on ConsoleKeyInfo which is specific to the terminal domain and
// won't be relevant or useful for a proper GUI frontend. Consider separating these concerns out
// so that the IChangeNotifier is a pure AutoResetEvent replacement and then a separate channel-based
// solution for nonblocking keyboard input.
public interface IChangeNotifier
{
    Task<ConsoleKeyInfo> WaitAsync(CancellationToken cancellationToken);

    void Set();

    void Set(ConsoleKeyInfo key);
}
