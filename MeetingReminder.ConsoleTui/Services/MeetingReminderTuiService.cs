using MeetingReminder.Application.UseCases;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Threading.Channels;

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
    private static readonly string[] SpinnerFrames = ["|", "/", "-", "\\"];
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);

    private readonly ChannelReader<CalendarEventsUpdated> _calendarChannelReader;
    private readonly ChannelReader<NotificationStateChanged> _notificationChannelReader;
    private readonly IKeyboardInputHandler _keyboardInputHandler;
    private readonly AcknowledgeMeeting _acknowledgeMeeting;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<MeetingReminderTuiService> _logger;
    private readonly TimeSpan _pollingInterval;

    // All state is only ever touched from the single event-loop task.
    private IReadOnlyList<MeetingEvent> _currentEvents = [];
    private IReadOnlyList<MeetingState> _activeNotifications = [];
    private DateTime _lastPollTime = DateTime.UtcNow;
    private int _spinnerIndex;
    private int _selectedMeetingIndex = -1; // -1 = auto-select next upcoming meeting

    public MeetingReminderTuiService(
        ChannelReader<CalendarEventsUpdated> calendarChannelReader,
        ChannelReader<NotificationStateChanged> notificationChannelReader,
        IKeyboardInputHandler keyboardInputHandler,
        AcknowledgeMeeting acknowledgeMeeting,
        IHostApplicationLifetime applicationLifetime,
        IAppConfiguration configuration,
        ITimeProvider timeProvider,
        ILogger<MeetingReminderTuiService> logger)
    {
        _calendarChannelReader = calendarChannelReader;
        _notificationChannelReader = notificationChannelReader;
        _keyboardInputHandler = keyboardInputHandler;
        _acknowledgeMeeting = acknowledgeMeeting;
        _applicationLifetime = applicationLifetime;
        _timeProvider = timeProvider;
        _pollingInterval = configuration.PollingInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meeting Reminder TUI service started");
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
                        DrainCalendarMessages();
                        DrainNotificationMessages();
                        await ProcessKeyboardInput(stoppingToken);

                        _spinnerIndex = (_spinnerIndex + 1) % SpinnerFrames.Length;

                        ctx.UpdateTarget(BuildDisplay());
                        await Task.Delay(TickInterval, stoppingToken);
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

    private void DrainCalendarMessages()
    {
        while (_calendarChannelReader.TryRead(out var calUpdate))
        {
            _currentEvents = calUpdate.AllEvents;
            _lastPollTime = calUpdate.OccurredAt;

            if (_selectedMeetingIndex >= _currentEvents.Count)
                _selectedMeetingIndex = -1;
        }
    }

    private void DrainNotificationMessages()
    {
        while (_notificationChannelReader.TryRead(out var notifUpdate))
        {
            _activeNotifications = notifUpdate.ActiveNotifications;
        }
    }

    private async Task ProcessKeyboardInput(CancellationToken stoppingToken)
    {
        if (!Console.KeyAvailable)
            return;

        var key = Console.ReadKey(intercept: true);
        var command = _keyboardInputHandler.MapKey(key);
        await HandleCommand(command, stoppingToken);
    }

    // -------------------------------------------------------------------------
    // Command handling
    // -------------------------------------------------------------------------

    private async Task HandleCommand(TuiCommand command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case TuiCommand.NavigateUp:
                NavigateUp();
                break;
            case TuiCommand.NavigateDown:
                NavigateDown();
                break;
            case TuiCommand.Acknowledge:
                await HandleAcknowledgeAsync(openLink: false, cancellationToken);
                break;
            case TuiCommand.OpenAndAcknowledge:
                await HandleAcknowledgeAsync(openLink: true, cancellationToken);
                break;
            case TuiCommand.Quit:
                _logger.LogInformation("Quit requested by user");
                _applicationLifetime.StopApplication();
                break;
        }
    }

    /// <summary>
    /// Handles command execution for testing purposes.
    /// </summary>
    internal async Task HandleCommandForTesting(TuiCommand command, CancellationToken cancellationToken)
    {
        await HandleCommand(command, cancellationToken);
    }

    private async Task HandleAcknowledgeAsync(bool openLink, CancellationToken cancellationToken)
    {
        var selectedMeeting = GetSelectedMeeting();
        if (selectedMeeting is null)
        {
            _logger.LogDebug("No meeting selected to acknowledge");
            return;
        }

        var ackCommand = new AcknowledgeMeetingCommand(selectedMeeting.Id, openLink);
        var result = await _acknowledgeMeeting.Acknowledge(ackCommand);

        result.Switch(
            _ => _logger.LogInformation(
                "Meeting acknowledged: {MeetingId}, OpenLink: {OpenLink}",
                selectedMeeting.Id, openLink),
            error => _logger.LogWarning(
                "Failed to acknowledge meeting {MeetingId}: {Error}",
                selectedMeeting.Id, error.Message));
    }

    // -------------------------------------------------------------------------
    // Navigation state
    // -------------------------------------------------------------------------

    public MeetingEvent? GetSelectedMeeting()
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

        var maxIndex = Math.Min(sorted.Count, MaxVisibleRows) - 1;

        if (_selectedMeetingIndex < 0)
            _selectedMeetingIndex = 0;
        else if (_selectedMeetingIndex < maxIndex)
            _selectedMeetingIndex++;
    }

    public int GetSelectedIndex() => _selectedMeetingIndex;

    internal void SetEventsForTesting(IReadOnlyList<MeetingEvent> events)
    {
        _currentEvents = events;
        if (_selectedMeetingIndex >= events.Count)
            _selectedMeetingIndex = -1;
    }

    private MeetingEvent? GetNextUpcomingMeeting(IReadOnlyList<MeetingEvent> sorted)
    {
        var now = _timeProvider.UtcNow;
        return sorted.FirstOrDefault(e => e.StartTime > now) ?? sorted.FirstOrDefault();
    }

    private IReadOnlyList<MeetingEvent> GetSortedEvents()
        => _currentEvents.OrderBy(e => e.StartTime).ToList();

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

        var spinner = SpinnerFrames[_spinnerIndex];
        var countdown = FormatCountdown(timeUntilNextPoll);

        return new Markup(
            $"[grey]Last updated: {_lastPollTime.ToLocalTime():HH:mm:ss}[/]  " +
            $"[cyan]{spinner}[/] [grey]Next poll in {countdown}[/]");
    }

    private static string FormatCountdown(TimeSpan t)
    {
        if (t.TotalSeconds < 1) return "now...";
        if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    private const int MaxVisibleRows = 5;

    private IRenderable BuildMeetingsPanel() =>
        new Panel(BuildEventsTable())
            .Header("[yellow]Upcoming Meetings[/]")
            .Border(BoxBorder.Rounded)
            .Expand();

    private IRenderable BuildEventsTable()
    {
        var table = CreateEventsTableStructure();

        if (_currentEvents.Count == 0)
        {
            PadBlankRows(table, MaxVisibleRows);
            return new Rows(
                table,
                new Markup("[yellow]No upcoming meetings in the next 7 days.[/]"));
        }

        var sorted = GetSortedEvents();
        var visible = sorted.Take(MaxVisibleRows).ToList();
        var selectedMeeting = GetSelectedMeeting();

        foreach (var evt in visible)
        {
            var isSelected = selectedMeeting?.Id == evt.Id;
            var notifState = _activeNotifications.FirstOrDefault(n => n.Event.Id == evt.Id);
            AddEventRow(table, evt, isSelected, notifState);
        }

        PadBlankRows(table, MaxVisibleRows - visible.Count);

        var selectionInfo = _selectedMeetingIndex >= 0
            ? $"Selected: {_selectedMeetingIndex + 1}/{visible.Count}"
            : $"Auto-selected next meeting ({sorted.Count} total)";

        return new Rows(
            table,
            new Markup($"[green]Found {_currentEvents.Count} upcoming event(s).[/] [grey]{selectionInfo}[/]"));
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

    private void AddEventRow(Table table, MeetingEvent evt, bool isSelected, MeetingState? notifState)
    {
        var indicator = isSelected ? "[cyan bold]>[/]" : " ";
        var start = evt.StartTime.ToLocalTime().ToString("ddd MMM dd HH:mm");
        var end = evt.EndTime.ToLocalTime().ToString("HH:mm");
        var title = Markup.Escape(TruncateString(evt.Title, 35));
        var link = evt.Link != null ? $"[green]{GetLinkTypeName(evt.Link)}[/]" : "[grey]-[/]";
        var status = GetStatusIndicator(notifState);

        if (isSelected)
        {
            title = $"[cyan]{title}[/]";
            start = $"[cyan]{start}[/]";
            end = $"[cyan]{end}[/]";
        }
        else if (notifState?.CurrentLevel >= NotificationLevel.Urgent)
        {
            title = $"[red]{title}[/]";
            start = $"[red]{start}[/]";
        }
        else if (notifState?.CurrentLevel >= NotificationLevel.Moderate)
        {
            title = $"[yellow]{title}[/]";
            start = $"[yellow]{start}[/]";
        }

        table.AddRow(indicator, start, end, title, link, status);
    }

    private static string GetStatusIndicator(MeetingState? state)
    {
        if (state == null) return "[grey]-[/]";
        if (state.IsAcknowledged) return "[green]OK Acknowledged[/]";
        return state.CurrentLevel switch
        {
            NotificationLevel.Critical => "[red]!! CRITICAL[/]",
            NotificationLevel.Urgent   => "[orange1]! Urgent[/]",
            NotificationLevel.Moderate => "[yellow]* Moderate[/]",
            NotificationLevel.Gentle   => "[blue]~ Gentle[/]",
            _                          => "[grey]-[/]"
        };
    }

    private static string GetLinkTypeName(MeetingLink link) => link switch
    {
        GoogleMeetLink => "Meet",
        ZoomLink => "Zoom",
        MicrosoftTeamsLink => "Teams",
        OtherLink => "Link",
        _ => "Link"
    };

    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length > maxLength ? value[..(maxLength - 3)] + "..." : value;
    }

    private static IRenderable BuildKeyboardHints()
    {
        return new Markup(
            "[grey]Enter[/] Acknowledge  " +
            "[grey]O[/] Open link  " +
            "[grey]Up/Down[/] Navigate  " +
            "[grey]Ctrl+C[/] Exit");
    }
}
