using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Input;
using MeetingReminder.Domain.Meetings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Main TUI service. Aggregates events from the system and displays state in a rich, responsive UI.
/// </summary>
public class UserInterfaceService : BackgroundService
{
    private const int _maxRows = 10;

    private readonly IMeetingRepository _meetings;
    private readonly InputCommandMapper _keyboardInputHandler;
    private readonly AcknowledgeMeeting _acknowledgeMeeting;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IChangeNotifier _changes;
    private readonly ITimeProvider _time;
    private readonly ILogger<UserInterfaceService> _logger;

    private int _selectedMeetingIndex = -1; // -1 = auto-select next upcoming meeting

    public UserInterfaceService(
        IMeetingRepository meetings,
        InputCommandMapper keyboardInputHandler,
        AcknowledgeMeeting acknowledgeMeeting,
        IHostApplicationLifetime applicationLifetime,
        IChangeNotifier changes,
        ITimeProvider time,
        ILogger<UserInterfaceService> logger)
    {
        _meetings = meetings;
        _keyboardInputHandler = keyboardInputHandler;
        _acknowledgeMeeting = acknowledgeMeeting;
        _applicationLifetime = applicationLifetime;
        _changes = changes;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.CursorVisible = false;

        var keyThread = new Thread(() => KeyPressThreadFunction(stoppingToken))
        {
            IsBackground = true,
            Name = "KeyboardReader"
        };
        keyThread.Start();

        try
        {
            await AnsiConsole.Live(new Markup("[yellow]Loading...[/]"))
                .AutoClear(false)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    var meetings = _meetings.GetOrderedUpcomingEvents();
                    SetupSelectedIndex(meetings);
                    ctx.UpdateTarget(InterfaceBuilder.BuildDisplay(meetings, _maxRows, _selectedMeetingIndex, _time.Now));
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var key = await _changes.WaitAsync(stoppingToken);
                        meetings = _meetings.GetOrderedUpcomingEvents();
                        if (key.Key != ConsoleKey.None)
                            await ProcessKeyboardInput(key, meetings, stoppingToken);
                        SetupSelectedIndex(meetings);
                        ctx.UpdateTarget(InterfaceBuilder.BuildDisplay(meetings, _maxRows, _selectedMeetingIndex, _time.Now));
                    }
                });
        }
        finally
        {
            Console.CursorVisible = true;
            keyThread.Join(1000);
        }
    }

    private void KeyPressThreadFunction(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var key = Console.ReadKey(intercept: true);
            _changes.Set(key);
        }
    }

    // -------------------------------------------------------------------------
    // Input Handling
    // -------------------------------------------------------------------------

    private async Task ProcessKeyboardInput(ConsoleKeyInfo key, IReadOnlyList<MeetingState> meetings, CancellationToken stoppingToken)
    {
        switch (_keyboardInputHandler.MapKey(key))
        {
            case InputCommand.NavigateUp:
                NavigateUp(meetings);
                break;

            case InputCommand.NavigateDown:
                NavigateDown(meetings);
                break;

            case InputCommand.Acknowledge:
                await HandleAcknowledgeAsync(meetings, openLink: false, stoppingToken);
                break;

            case InputCommand.OpenAndAcknowledge:
                await HandleAcknowledgeAsync(meetings, openLink: true, stoppingToken);
                break;

            case InputCommand.Quit:
                _logger.LogInformation("Quit requested by user");
                _applicationLifetime.StopApplication();
                break;
        }
    }

    private async Task HandleAcknowledgeAsync(IReadOnlyList<MeetingState> meetings, bool openLink, CancellationToken cancellationToken)
    {
        var selectedMeeting = GetSelectedMeeting(meetings);
        if (selectedMeeting.Event is not null)
            await _acknowledgeMeeting.Acknowledge(new AcknowledgeMeetingCommand(selectedMeeting.Event.Id, openLink));
    }

    private MeetingState GetSelectedMeeting(IReadOnlyList<MeetingState> meetings)
    {
        if (meetings.Count == 0)
            return default;

        if (_selectedMeetingIndex >= 0 && _selectedMeetingIndex < meetings.Count)
            return meetings[_selectedMeetingIndex];

        return default;
    }

    private void SetupSelectedIndex(IReadOnlyList<MeetingState> meetings)
    {
        if (_selectedMeetingIndex >= 0 && _selectedMeetingIndex < meetings.Count)
            return;
        if (_selectedMeetingIndex < 0)
        {
            _selectedMeetingIndex = 0;
            return;
        }
        if (_selectedMeetingIndex >= meetings.Count)
        {
            _selectedMeetingIndex = meetings.Count - 1;
            return;
        }
    }

    private void NavigateUp(IReadOnlyList<MeetingState> meetings)
    {
        if (meetings.Count == 0)
            return;

        if (_selectedMeetingIndex > 0)
            _selectedMeetingIndex--;
    }

    private void NavigateDown(IReadOnlyList<MeetingState> meetings)
    {
        if (meetings.Count == 0)
            return;

        var maxIndex = Math.Min(meetings.Count, _maxRows) - 1;

        if (_selectedMeetingIndex < maxIndex)
            _selectedMeetingIndex++;
    }
}
