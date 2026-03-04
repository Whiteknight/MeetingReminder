using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Input;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Main TUI service. Owns a single event loop that:
///   1. Drains pending channel messages (calendar updates, notification updates)
///   2. Reads one pending keypress, maps it to a TuiCommand, and handles it
///   3. Renders the display once
///
/// All three steps happen on the same thread, so there is no contention
/// between Spectre.Console's Live display and Console.ReadKey.
/// </summary>
public class MeetingReminderTuiService : BackgroundService
{
    // TODO: Separate out the UI-building and rendering from the other timer-based logic
    private const int _maxRows = 10;

    private static readonly string[] _spinnerFrames = ["|", "/", "-", "\\"];

    //private static readonly TimeSpan _tick = TimeSpan.FromMilliseconds(1000);
    private readonly IMeetingRepository _meetings;

    private readonly IKeyboardInputHandler _keyboardInputHandler;
    private readonly AcknowledgeMeeting _acknowledgeMeeting;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITimeProvider _timeProvider;
    private readonly IChangeNotifier _changes;
    private readonly ILogger<MeetingReminderTuiService> _logger;

    private int _spinnerIndex;
    private int _selectedMeetingIndex = -1; // -1 = auto-select next upcoming meeting

    public MeetingReminderTuiService(
        IMeetingRepository meetings,
        IKeyboardInputHandler keyboardInputHandler,
        AcknowledgeMeeting acknowledgeMeeting,
        IHostApplicationLifetime applicationLifetime,
        IAppConfiguration configuration,
        ITimeProvider timeProvider,
        IChangeNotifier changes,
        ILogger<MeetingReminderTuiService> logger)
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
            var initial = GetSortedEvents();
            await AnsiConsole.Live(BuildDisplay(_spinnerIndex, initial, _selectedMeetingIndex))
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await WaitForUpdateEvent(stoppingToken);
                        while (Console.KeyAvailable)
                            await ProcessKeyboardInput(stoppingToken);

                        _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;

                        var meetings = GetSortedEvents();
                        ctx.UpdateTarget(BuildDisplay(_spinnerIndex, meetings, _selectedMeetingIndex));
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

    private async Task WaitForUpdateEvent(CancellationToken cancellationToken)
    {
        var timeout = Task.Delay(TimeSpan.FromMilliseconds(1000), cancellationToken);
        var change = _changes.WaitAsync(cancellationToken);
        await Task.WhenAny(timeout, change);
    }

    // -------------------------------------------------------------------------
    // Event loop helpers
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

    // -------------------------------------------------------------------------
    // Command handling
    // -------------------------------------------------------------------------

    private async Task HandleAcknowledgeAsync(bool openLink, CancellationToken cancellationToken)
    {
        var selectedMeeting = GetSelectedMeeting();
        if (selectedMeeting.Event is null)
        {
            _logger.LogDebug("No meeting selected to acknowledge");
            return;
        }

        var ackCommand = new AcknowledgeMeetingCommand(selectedMeeting.Event.Id, openLink);
        var result = await _acknowledgeMeeting.Acknowledge(ackCommand);

        result.Switch(
            _ => _logger.LogInformation(
                "Meeting acknowledged: {MeetingId}, OpenLink: {OpenLink}",
                selectedMeeting.Event.Id, openLink),
            error => _logger.LogWarning(
                "Failed to acknowledge meeting {MeetingId}: {Error}",
                selectedMeeting.Event.Id, error.Message));
    }

    // -------------------------------------------------------------------------
    // Navigation state
    // -------------------------------------------------------------------------

    public MeetingState GetSelectedMeeting()
    {
        var sorted = GetSortedEvents();
        if (sorted.Count == 0)
            return default;

        if (_selectedMeetingIndex >= 0 && _selectedMeetingIndex < sorted.Count)
            return sorted[_selectedMeetingIndex];

        return GetNextUpcomingMeeting(sorted);
    }

    public void NavigateUp()
    {
        var sorted = GetSortedEvents();
        if (sorted.Count == 0)
            return;

        if (_selectedMeetingIndex < 0)
            _selectedMeetingIndex = 0;
        else if (_selectedMeetingIndex > 0)
            _selectedMeetingIndex--;
    }

    public void NavigateDown()
    {
        var sorted = GetSortedEvents();
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

    private IReadOnlyList<MeetingState> GetSortedEvents()
        => _meetings.GetAll().Match(
            events => events.OrderBy(e => e.Event.StartTime).ToList(),
            _ => []);

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private static IRenderable BuildDisplay(int tick, IReadOnlyList<MeetingState> meetings, int selectedMeetingIndex)
    {
        return new Rows(
            BuildHeader(tick),
            BuildMeetingsPanel(meetings, selectedMeetingIndex),
            BuildKeyboardHints());
    }

    private static IRenderable BuildHeader(int tick)
    {
        var spinner = _spinnerFrames[tick % _spinnerFrames.Length];
        return new Markup($"[grey]Meeting monitor[/] [cyan]{spinner}[/]");
    }

    private static IRenderable BuildMeetingsPanel(IReadOnlyList<MeetingState> meetings, int selectedMeetingIndex) =>
        new Panel(BuildEventsTable(meetings, selectedMeetingIndex))
            .Header("[yellow]Upcoming Meetings[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DarkSlateGray1)
            .Expand();

    private static IRenderable BuildEventsTable(IReadOnlyList<MeetingState> meetings, int selectedMeetingIndex)
    {
        var table = CreateEventsTableStructure();
        if (meetings.Count == 0)
        {
            PadBlankRows(table, _maxRows);
            return new Rows(
                table,
                new Markup("[yellow]No upcoming meetings in the next 7 days.[/]"));
        }

        if (selectedMeetingIndex < 0)
            selectedMeetingIndex = 0;

        var visibleMeetings = meetings.Take(_maxRows).ToList();
        var selectedIndex = selectedMeetingIndex >= 0 && selectedMeetingIndex < meetings.Count ? selectedMeetingIndex : 0;

        for (int i = 0; i < visibleMeetings.Count; i++)
        {
            var meeting = visibleMeetings[i];
            var isSelected = i == selectedIndex;
            AddEventRow(table, meeting, isSelected);
        }

        PadBlankRows(table, _maxRows - visibleMeetings.Count);

        var selectionInfo = selectedMeetingIndex >= 0
            ? $"Selected: {selectedMeetingIndex + 1}/{visibleMeetings.Count}"
            : $"Auto-selected next meeting ({meetings.Count} total)";

        return new Rows(
            table,
            new Markup($"[green]Found {meetings.Count} upcoming event(s).[/] [grey]{selectionInfo}[/]"));
    }

    private static Table CreateEventsTableStructure()
    {
        var table = new Table();
        table.Border(TableBorder.Simple);
        table.Expand();
        table.AddColumn(new TableColumn("").Width(3));
        table.AddColumn("Start Time");
        table.AddColumn("End Time");
        table.AddColumn("Title");
        table.AddColumn("Link");
        table.AddColumn("Status");
        return table;
    }

    private static void PadBlankRows(Table table, int count)
    {
        for (var i = 0; i < count; i++)
            table.AddRow(" ", " ", " ", " ", " ", " ");
    }

    private static void AddEventRow(Table table, MeetingState meeting, bool isSelected)
    {
        var indicator = isSelected ? "[cyan bold]>[/]" : " ";
        var start = meeting.Event.StartTime.ToLocalTime().ToString("ddd MMM dd HH:mm");
        var end = meeting.Event.EndTime.ToLocalTime().ToString("HH:mm");
        var title = Markup.Escape(TruncateString(meeting.Event.Title, 35));
        var link = meeting.Event.Link != null ? $"[green]{GetLinkTypeName(meeting.Event.Link)}[/]" : "[grey]-[/]";
        var status = GetStatusIndicator(meeting);

        if (meeting.CurrentLevel >= NotificationLevel.Urgent)
        {
            title = $"[red]{title}[/]";
            start = $"[red]{start}[/]";
        }
        else if (meeting.CurrentLevel >= NotificationLevel.Moderate)
        {
            title = $"[yellow]{title}[/]";
            start = $"[yellow]{start}[/]";
        }

        table.AddRow(indicator, start, end, title, link, status);
    }

    private static string GetStatusIndicator(MeetingState state)
    {
        if (state.Event == null)
            return "[grey]-[/]";
        if (state.IsAcknowledged)
            return "[green]OK Acknowledged[/]";
        return state.CurrentLevel switch
        {
            NotificationLevel.Critical => "[red bold]!! STARTED !![/]",
            NotificationLevel.Urgent => "[orange1]! Starting ![/]",
            NotificationLevel.Moderate => "[yellow]Get Ready[/]",
            NotificationLevel.Gentle => "[blue]On Deck[/]",
            _ => "[grey]-[/]"
        };
    }

    private static string GetLinkTypeName(MeetingLink link)
        => link switch
        {
            GoogleMeetLink => "Meet",
            ZoomLink => "Zoom",
            MicrosoftTeamsLink => "Teams",
            OtherLink => "Link",
            _ => "Link"
        };

    private static string TruncateString(string value, int maxLength)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Length > maxLength ? value[..(maxLength - 3)] + "..." : value;

    private static IRenderable BuildKeyboardHints()
        => new Markup(
            "[grey]Enter[/] Acknowledge  " +
            "[grey]O[/] Open link  " +
            "[grey]Up/Down[/] Navigate  " +
            "[grey]Ctrl+C[/] Exit");
}
