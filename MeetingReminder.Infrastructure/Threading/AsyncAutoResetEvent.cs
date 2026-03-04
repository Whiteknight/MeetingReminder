using System.Threading.Channels;
using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Threading;

public sealed class AsyncAutoResetEvent : IChangeNotifier
{
    private readonly Channel<bool> _channel;

    public AsyncAutoResetEvent()
    {
        _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(10)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken).AsTask();
    }

    public void Set()
    {
        _channel.Writer.TryWrite(true);
    }
}
