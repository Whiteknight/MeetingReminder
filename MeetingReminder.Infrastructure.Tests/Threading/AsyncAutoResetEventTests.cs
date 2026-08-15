using AwesomeAssertions;
using MeetingReminder.Infrastructure.Threading;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Threading;

[TestFixture]
public class AsyncAutoResetEventTests
{
    private AsyncAutoResetEvent _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new AsyncAutoResetEvent();

    // -------------------------------------------------------------------------
    // Timeout behaviour
    // -------------------------------------------------------------------------

    [Test]
    public async Task WaitAsync_WithTimeout_ReturnsDefaultWhenNoSignal()
    {
        var result = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(50));

        result.Should().Be(default(ConsoleKeyInfo));
    }

    [Test]
    public async Task WaitAsync_WithTimeout_ReturnsSignalledKeyBeforeTimeout()
    {
        var expected = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);

        // Signal after a short delay - well within the timeout
        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            _sut.Set(expected);
        });

        var result = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(500));

        result.Should().Be(expected);
    }

    [Test]
    public async Task WaitAsync_WithZeroTimeout_WaitsIndefinitely_UntilSignalled()
    {
        // default(TimeSpan) == TimeSpan.Zero should mean "wait forever"
        var expected = new ConsoleKeyInfo('B', ConsoleKey.B, false, false, false);

        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            _sut.Set(expected);
        });

        var result = await _sut.WaitAsync(CancellationToken.None);

        result.Should().Be(expected);
    }

    [Test]
    public async Task WaitAsync_WithTimeout_ReturnedDefaultHasNoKeyPressed()
    {
        var result = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(50));

        // default(ConsoleKeyInfo).Key is ConsoleKey.None (0)
        result.Key.Should().Be(ConsoleKey.None);
    }

    [Test]
    public async Task WaitAsync_CancelledBeforeTimeout_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.WaitAsync(cts.Token, TimeSpan.FromSeconds(10));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task WaitAsync_CancelledDuringWait_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            cts.Cancel();
        });

        var act = async () => await _sut.WaitAsync(cts.Token, TimeSpan.FromSeconds(10));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // -------------------------------------------------------------------------
    // Set / Set(key) behaviour
    // -------------------------------------------------------------------------

    [Test]
    public async Task Set_WithNoKey_SignalsDefaultConsoleKeyInfo()
    {
        _sut.Set();

        var result = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(200));

        result.Should().Be(default(ConsoleKeyInfo));
    }

    [Test]
    public async Task Set_WithKey_SignalsCorrectKey()
    {
        var expected = new ConsoleKeyInfo('Q', ConsoleKey.Q, false, false, false);

        _sut.Set(expected);

        var result = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(200));

        result.Should().Be(expected);
    }

    [Test]
    public async Task MultipleSet_ConsumesOneSignalPerWait()
    {
        var first = new ConsoleKeyInfo('1', ConsoleKey.D1, false, false, false);
        var second = new ConsoleKeyInfo('2', ConsoleKey.D2, false, false, false);

        _sut.Set(first);
        _sut.Set(second);

        var r1 = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(200));
        var r2 = await _sut.WaitAsync(CancellationToken.None, TimeSpan.FromMilliseconds(200));

        r1.Should().Be(first);
        r2.Should().Be(second);
    }
}
