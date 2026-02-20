using System.Threading.Channels;

namespace MeetingReminder.Infrastructure.Channels;

/// <summary>
/// A ChannelWriter that fans out each written item to multiple downstream writers.
/// This allows a single producer to publish to multiple independent consumers,
/// each with their own channel and reader.
/// </summary>
public sealed class FanOutChannelWriter<T> : ChannelWriter<T>
{
    private readonly ChannelWriter<T>[] _writers;

    public FanOutChannelWriter(params ChannelWriter<T>[] writers)
    {
        _writers = writers;
    }

    public override bool TryWrite(T item)
    {
        var allSucceeded = true;
        foreach (var writer in _writers)
        {
            if (!writer.TryWrite(item))
                allSucceeded = false;
        }
        return allSucceeded;
    }

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(true);
    }
}
