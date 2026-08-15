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
        if (timeout <= TimeSpan.Zero)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            // WaitToReadAsync suspends until an item is available or the token fires.
            // It does NOT consume the item, so a subsequent TryRead is the only active read.
            await _channel.Reader.WaitToReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Distinguish between caller cancellation (propagate) and our own timeout (return default).
            cancellationToken.ThrowIfCancellationRequested();
            return default; // Timeout elapsed - caller should redraw without a key
        }

        _channel.Reader.TryRead(out var key);
        return key;
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
