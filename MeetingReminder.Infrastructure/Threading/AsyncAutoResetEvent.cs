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

    public Task<ConsoleKeyInfo> WaitAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken).AsTask();
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
