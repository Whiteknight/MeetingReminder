namespace MeetingReminder.Domain;

public interface IChangeNotifier
{
    Task<ConsoleKeyInfo> WaitAsync(CancellationToken cancellationToken);

    void Set();

    void Set(ConsoleKeyInfo key);
}
