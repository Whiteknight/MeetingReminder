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
    private const int _maxRows = 5;

    private static readonly string[] _spinnerFrames = ["|", "/", "-", "\\"];
    private static readonly TimeSpan _tick = TimeSpan.FromMilliseconds(100);
    private readonly IMeetingRepository _meetings;
    private readonly IKeyboardInputHandler _keyboardInputHandler;
    private readonly AcknowledgeMeeting _acknowledgeMeeting;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<MeetingReminderTuiService> _logger;
    private readonly TimeSpan _pollingInterval;

    private DateTime _lastPollTime = DateTime.UtcNow;
    private int _spinnerIndex;
    private int _selectedMeetingIndex = -1; // -1 = auto-select next upcoming meeting

    public MeetingReminderTuiService(
        IMeetingRepository meetings,
        IKeyboardInputHandler keyboardInputHandler,
        AcknowledgeMeeting acknowledgeMeeting,
        IHostApplicationLifetime applicationLifetime,
        IAppConfiguration configuration,
        ITimeProvider timeProvider,
        ILogger<MeetingReminderTuiService> logger)
    {
        _meetings = meetings;
        _keyboardInputHandler = keyboardInputHandler;
        _acknowledgeMeeting = acknowledgeMeeting;
        _applicationLifetime = applicationLifetime;
        _timeProvider = timeProvider;
        _pollingInterval = configuration.PollingInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.CursorVisible = false;

        try
        {
            await AnsiConsole.Live(BuildDisplay())
                .AutoClear(true)
                .Overflow(VerticalOverflow.Ellipsis)
                .StartAsync(async ctx =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await ProcessKeyboardInput(stoppingToken);

                        _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;

                        ctx.UpdateTarget(BuildDisplay());
                        await Task.Delay(_tick, stoppingToken);
                    }
                });
        }
        finally
        {
            Console.CursorVisible = true;
        }
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
        if (selectedMeeting is null)
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

    public MeetingState? GetSelectedMeeting()
    {
        var sorted = GetSortedEvents();
        if (sorted.Count == 0)
            return null;

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

    public int GetSelectedIndex() => _selectedMeetingIndex;

    private MeetingState? GetNextUpcomingMeeting(IReadOnlyList<MeetingState> sorted)
    {
        var now = _timeProvider.UtcNow;
        return sorted.FirstOrDefault(e => e.Event.StartTime > now) ?? sorted.FirstOrDefault();
    }

    private IReadOnlyList<MeetingState> GetSortedEvents()
        => _meetings.GetAll().Match(
            events => events.OrderBy(e => e.Event.StartTime).ToList(),
            _ => []);

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private IRenderable BuildDisplay()
    {
        return new Rows(
            BuildHeader(),
            BuildMeetingsPanel(),
            BuildKeyboardHints());
    }

    private IRenderable BuildHeader()
    {
        var nextPollTime = _lastPollTime + _pollingInterval;
        var timeUntilNextPoll = nextPollTime - _timeProvider.UtcNow;
        if (timeUntilNextPoll < TimeSpan.Zero)
            timeUntilNextPoll = TimeSpan.Zero;

        var spinner = _spinnerFrames[_spinnerIndex];
        var countdown = FormatCountdown(timeUntilNextPoll);

        return new Markup(
            $"[grey]Last updated: {_lastPollTime.ToLocalTime():HH:mm:ss}[/]  " +
            $"[cyan]{spinner}[/] [grey]Next poll in {countdown}[/]");
    }

    private static string FormatCountdown(TimeSpan t)
    {
        if (t.TotalSeconds < 1)
            return "now...";
        if (t.TotalMinutes >= 1)
            return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    private IRenderable BuildMeetingsPanel() =>
        new Panel(BuildEventsTable())
            .Header("[yellow]Upcoming Meetings[/]")
            .Border(BoxBorder.Rounded)
            .Expand();

    private IRenderable BuildEventsTable()
    {
        var table = CreateEventsTableStructure();

        var sorted = GetSortedEvents();
        if (sorted.Count == 0)
        {
            PadBlankRows(table, _maxRows);
            return new Rows(
                table,
                new Markup("[yellow]No upcoming meetings in the next 7 days.[/]"));
        }

        var visibleMeetings = sorted.Take(_maxRows).ToList();
        var selectedIndex = _selectedMeetingIndex >= 0 && _selectedMeetingIndex < sorted.Count ? _selectedMeetingIndex : 0;

        for (int i = 0; i < visibleMeetings.Count; i++)
        {
            var meeting = visibleMeetings[i];
            var isSelected = i == selectedIndex;
            AddEventRow(table, meeting, isSelected);
        }

        PadBlankRows(table, _maxRows - visibleMeetings.Count);

        var selectionInfo = _selectedMeetingIndex >= 0
            ? $"Selected: {_selectedMeetingIndex + 1}/{visibleMeetings.Count}"
            : $"Auto-selected next meeting ({sorted.Count} total)";

        return new Rows(
            table,
            new Markup($"[green]Found {sorted.Count} upcoming event(s).[/] [grey]{selectionInfo}[/]"));
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

        if (isSelected)
        {
            title = $"[cyan]{title}[/]";
            start = $"[cyan]{start}[/]";
            end = $"[cyan]{end}[/]";
        }
        else if (meeting?.CurrentLevel >= NotificationLevel.Urgent)
        {
            title = $"[red]{title}[/]";
            start = $"[red]{start}[/]";
        }
        else if (meeting?.CurrentLevel >= NotificationLevel.Moderate)
        {
            title = $"[yellow]{title}[/]";
            start = $"[yellow]{start}[/]";
        }

        table.AddRow(indicator, start, end, title, link, status);
    }

    private static string GetStatusIndicator(MeetingState? state)
    {
        if (state == null)
            return "[grey]-[/]";
        if (state.IsAcknowledged)
            return "[green]OK Acknowledged[/]";
        return state.CurrentLevel switch
        {
            NotificationLevel.Critical => "[red]!! CRITICAL[/]",
            NotificationLevel.Urgent => "[orange1]! Urgent[/]",
            NotificationLevel.Moderate => "[yellow]* Moderate[/]",
            NotificationLevel.Gentle => "[blue]~ Gentle[/]",
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
