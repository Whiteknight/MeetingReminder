using AwesomeAssertions;
using MeetingReminder.Domain.Input;
using NUnit.Framework;

namespace MeetingReminder.ConsoleTui.Tests.Services;

[TestFixture]
public class KeyboardInputServiceTests
{
    [Test]
    public void UpArrow_ReturnsNavigateUp()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.NavigateUp>();
    }

    [Test]
    public void DownArrow_ReturnsNavigateDown()
    {
        var key = new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.NavigateDown>();
    }

    [Test]
    public void Enter_ReturnsAcknowledge()
    {
        var key = new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.Acknowledge>();
    }

    [Test]
    public void OKey_ReturnsOpenAndAcknowledge()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.OpenAndAcknowledge>();
    }

    [Test]
    public void CtrlO_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('o', ConsoleKey.O, false, false, true);
        key.MapToInputCommand().Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void CKey_ReturnsUnacknowledge()
    {
        var key = new ConsoleKeyInfo('c', ConsoleKey.C, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.Unacknowledge>();
    }

    [Test]
    public void CtrlC_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('c', ConsoleKey.C, false, false, true);
        key.MapToInputCommand().Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void SKey_ReturnsSilence()
    {
        var key = new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.Silence>();
    }

    [Test]
    public void CtrlS_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('s', ConsoleKey.S, false, false, true);
        key.MapToInputCommand().Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void QKey_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.Quit>();
    }

    [Test]
    public void ShiftQ_ReturnsQuit()
    {
        var key = new ConsoleKeyInfo('Q', ConsoleKey.Q, true, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.Quit>();
    }

    [Test]
    public void CtrlQ_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, true);
        key.MapToInputCommand().Should().BeOfType<InputCommand.None>();
    }

    [Test]
    public void UnhandledKey_ReturnsNone()
    {
        var key = new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false);
        key.MapToInputCommand().Should().BeOfType<InputCommand.None>();
    }
}
