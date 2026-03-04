using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Threading;

public sealed class AsyncAutoResetEvent : IChangeNotifier
{
    private volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return _tcs.Task.WaitAsync(cancellationToken);
    }

    public void Set()
    {
        var tcs = _tcs;
        Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), tcs);
        tcs.TrySetResult(true);
    }
}
