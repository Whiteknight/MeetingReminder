using AwesomeAssertions;
using MeetingReminder.ConsoleTui.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace MeetingReminder.ConsoleTui.Tests.Services;

[TestFixture]
public class KeyboardInputServiceTests
{
    private KeyboardInputService _service = null!;

    [SetUp]
    public void SetUp()
    {
        var logger = Substitute.For<ILogger<KeyboardInputService>>();
        _service = new KeyboardInputService(logger);
    }

    [Test]
    public void UpArrow_ReturnsNavigateUp()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.NavigateUp>();
    }

    [Test]
    public void DownArrow_ReturnsNavigateDown()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.NavigateDown>();
    }

    [Test]
    public void Enter_ReturnsAcknowledge()
    {
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.Acknowledge>();
    }

    [Test]
    public void OKey_ReturnsOpenAndAcknowledge()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.OpenAndAcknowledge>();
    }

    [Test]
    public void CtrlO_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, true);
        _service.MapKey(key).Should().BeOfType<TuiCommand.None>();
    }

    [Test]
    public void QKey_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.Quit>();
    }

    [Test]
    public void ShiftQ_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('Q', ConsoleKey.Q, true, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.Quit>();
    }

    [Test]
    public void CtrlQ_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, true);
        _service.MapKey(key).Should().BeOfType<TuiCommand.None>();
    }

    [Test]
    public void UnhandledKey_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false);
        _service.MapKey(key).Should().BeOfType<TuiCommand.None>();
    }
}
