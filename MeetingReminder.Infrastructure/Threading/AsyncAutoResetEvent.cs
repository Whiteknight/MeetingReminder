using System.Threading.Channels;
using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Threading;

public sealed class AsyncAutoResetEvent : IChangeNotifier
{
    private readonly Channel<ConsoleKeyInfo> _channel;

    public AsyncAutoResetEvent()
    {
        _channel = Channel.CreateBounded<ConsoleKeyInfo>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <inheritdoc />
    public async Task<ConsoleKeyInfo> WaitAsync(CancellationToken cancellationToken, TimeSpan timeout = default)
    {
        var readTask = _channel.Reader.ReadAsync(cancellationToken).AsTask();

        if (timeout <= TimeSpan.Zero)
            return await readTask;

        var timeoutTask = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(readTask, timeoutTask);

        // If the cancellation token fired, timeoutTask will be the completed task (as cancelled).
        // Propagate cancellation by checking the token before returning a default value.
        cancellationToken.ThrowIfCancellationRequested();

        if (completed == timeoutTask)
            return default; // Timeout elapsed - caller should redraw without a key

        return await readTask; // Already completed; unwrap to propagate any exception
    }

    public void Set()
    {
        _channel.Writer.TryWrite(default);
    }

    public void Set(ConsoleKeyInfo key)
    {
        _channel.Writer.TryWrite(key);
    }
}
