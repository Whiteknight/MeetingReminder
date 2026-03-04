namespace MeetingReminder.Domain;

public interface IChangeNotifier
{
    Task WaitAsync(CancellationToken cancellationToken);

    void Set();
}
