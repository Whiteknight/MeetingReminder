using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
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
    private readonly IKeyboardInputHandler _keyboardInputHandler;
    private readonly AcknowledgeMeeting _acknowledgeMeeting;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITimeProvider _timeProvider;
    private readonly IChangeNotifier _changes;
    private readonly ILogger<UserInterfaceService> _logger;

    private int _selectedMeetingIndex = -1; // -1 = auto-select next upcoming meeting

    public UserInterfaceService(
        IMeetingRepository meetings,
        IKeyboardInputHandler keyboardInputHandler,
        AcknowledgeMeeting acknowledgeMeeting,
        IHostApplicationLifetime applicationLifetime,
        IAppConfiguration configuration,
        ITimeProvider timeProvider,
        IChangeNotifier changes,
        ILogger<UserInterfaceService> logger)
    {
        _meetings = meetings;
        _keyboardInputHandler = keyboardInputHandler;
        _acknowledgeMeeting = acknowledgeMeeting;
        _applicationLifetime = applicationLifetime;
        _timeProvider = timeProvider;
        _changes = changes;
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
            var initial = _meetings.GetOrderedUpcomingEvents();
            await AnsiConsole.Live(InterfaceBuilder.BuildDisplay(initial, _maxRows, _selectedMeetingIndex))
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await _changes.WaitAsync(stoppingToken);
                        while (Console.KeyAvailable)
                            await ProcessKeyboardInput(stoppingToken);

                        var meetings = _meetings.GetOrderedUpcomingEvents();
                        ctx.UpdateTarget(InterfaceBuilder.BuildDisplay(meetings, _maxRows, _selectedMeetingIndex));
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
            if (Console.KeyAvailable)
                _changes.Set();
        }
    }

    // -------------------------------------------------------------------------
    // Input Handling
    // -------------------------------------------------------------------------

    private async Task ProcessKeyboardInput(CancellationToken stoppingToken)
    {
        if (!Console.KeyAvailable)
            return;

        var key = Console.ReadKey(intercept: true);
        switch (_keyboardInputHandler.MapKey(key))
        {
            case InputCommand.NavigateUp:
                NavigateUp();
                break;

            case InputCommand.NavigateDown:
                NavigateDown();
                break;

            case InputCommand.Acknowledge:
                await HandleAcknowledgeAsync(openLink: false, stoppingToken);
                break;

            case InputCommand.OpenAndAcknowledge:
                await HandleAcknowledgeAsync(openLink: true, stoppingToken);
                break;

            case InputCommand.Quit:
                _logger.LogInformation("Quit requested by user");
                _applicationLifetime.StopApplication();
                break;
        }
    }

    private async Task HandleAcknowledgeAsync(bool openLink, CancellationToken cancellationToken)
    {
        var selectedMeeting = GetSelectedMeeting();
        if (selectedMeeting.Event is null)
            return;

        await _acknowledgeMeeting.Acknowledge(new AcknowledgeMeetingCommand(selectedMeeting.Event.Id, openLink));
    }

    public MeetingState GetSelectedMeeting()
    {
        var sorted = _meetings.GetOrderedUpcomingEvents();
        if (sorted.Count == 0)
            return default;

        if (_selectedMeetingIndex >= 0 && _selectedMeetingIndex < sorted.Count)
            return sorted[_selectedMeetingIndex];

        return GetNextUpcomingMeeting(sorted);
    }

    public void NavigateUp()
    {
        var sorted = _meetings.GetOrderedUpcomingEvents();
        if (sorted.Count == 0)
            return;

        if (_selectedMeetingIndex < 0)
            _selectedMeetingIndex = 0;
        else if (_selectedMeetingIndex > 0)
            _selectedMeetingIndex--;
    }

    public void NavigateDown()
    {
        var sorted = _meetings.GetOrderedUpcomingEvents();
        if (sorted.Count == 0)
            return;

        var maxIndex = Math.Min(sorted.Count, _maxRows) - 1;

        if (_selectedMeetingIndex < 0)
            _selectedMeetingIndex = 0;
        else if (_selectedMeetingIndex < maxIndex)
            _selectedMeetingIndex++;
    }

    private MeetingState GetNextUpcomingMeeting(IReadOnlyList<MeetingState> sorted)
    {
        var now = _timeProvider.UtcNow;
        return sorted.FirstOrDefault(e => e.Event.StartTime > now);
    }
}
