using AwesomeAssertions;
using MeetingReminder.Domain.Input;
using NUnit.Framework;

namespace MeetingReminder.ConsoleTui.Tests.Services;

[TestFixture]
public class KeyboardInputServiceTests
{
    private InputCommandMapper _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new InputCommandMapper();
    }

    [Test]
    public void UpArrow_ReturnsNavigateUp()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.NavigateUp>();
    }

    [Test]
    public void DownArrow_ReturnsNavigateDown()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.NavigateDown>();
    }

    [Test]
    public void Enter_ReturnsAcknowledge()
    {
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.Acknowledge>();
    }

    [Test]
    public void OKey_ReturnsOpenAndAcknowledge()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.OpenAndAcknowledge>();
    }

    [Test]
    public void CtrlO_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, true);
        _service.MapKey(key).Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void QKey_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.Quit>();
    }

    [Test]
    public void ShiftQ_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('Q', ConsoleKey.Q, true, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.Quit>();
    }

    [Test]
    public void CtrlQ_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, true);
        _service.MapKey(key).Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void UnhandledKey_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false);
        _service.MapKey(key).Should().BeOfType<InputCommand.None>();
    }
}
