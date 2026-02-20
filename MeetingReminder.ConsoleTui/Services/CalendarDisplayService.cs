using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Threading.Channels;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Hosted service that reads calendar events from the channel and updates the TUI display.
/// Uses Spectre.Console Live display for flicker-free updates.
/// Shows a countdown to the next poll with a spinner that updates every 5 seconds.
/// </summary>
public class CalendarDisplayService : BackgroundService
{
    private static readonly string[] SpinnerFrames = ["|", "/", "-", "\\"];

    private readonly ChannelReader<CalendarEventsUpdated> _channelReader;
    private readonly ILogger<CalendarDisplayService> _logger;
    private readonly TimeSpan _pollingInterval;

    private IReadOnlyList<MeetingEvent> _currentEvents = [];
    private DateTime _lastPollTime = DateTime.UtcNow;
    private int _spinnerIndex;

    public CalendarDisplayService(
        ChannelReader<CalendarEventsUpdated> channelReader,
        IAppConfiguration configuration,
        ILogger<CalendarDisplayService> logger)
    {
        _channelReader = channelReader;
        _pollingInterval = configuration.PollingInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Calendar display service started");

        await AnsiConsole.Live(BuildDisplay())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // Start a background task to read channel updates
                var channelTask = Task.Run(async () =>
                {
                    await foreach (var update in _channelReader.ReadAllAsync(stoppingToken))
                    {
                        _currentEvents = update.AllEvents;
                        _lastPollTime = update.OccurredAt;
                        ctx.UpdateTarget(BuildDisplay());
                    }
                }, stoppingToken);

                // Update countdown every 5 seconds
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    _spinnerIndex = (_spinnerIndex + 1) % SpinnerFrames.Length;
                    ctx.UpdateTarget(BuildDisplay());
                }
            });
    }

    private IRenderable BuildDisplay()
    {
        var rows = new Rows(
            BuildHeader(),
            new Text(""),
            BuildEventsTable(),
            new Text(""),
            BuildFooter());

        return new Panel(rows)
            .Header("[blue]Meeting Reminder TUI[/]")
            .Border(BoxBorder.Rounded)
            .Expand();
    }

    private IRenderable BuildHeader()
    {
        var nextPollTime = _lastPollTime + _pollingInterval;
        var timeUntilNextPoll = nextPollTime - DateTime.UtcNow;

        if (timeUntilNextPoll < TimeSpan.Zero)
            timeUntilNextPoll = TimeSpan.Zero;

        var spinner = SpinnerFrames[_spinnerIndex];
        var countdown = FormatCountdown(timeUntilNextPoll);

        return new Markup($"[grey]Last updated: {_lastPollTime.ToLocalTime():HH:mm:ss}[/]  [cyan]{spinner}[/] [grey]Next poll in {countdown}[/]");
    }

    private static string FormatCountdown(TimeSpan timeSpan)
    {
        if (timeSpan.TotalSeconds < 1)
            return "now...";

        if (timeSpan.TotalMinutes >= 1)
            return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";

        return $"{timeSpan.Seconds}s";
    }

    private IRenderable BuildEventsTable()
    {
        if (_currentEvents.Count == 0)
        {
            return new Markup("[yellow]No upcoming meetings in the next 7 days.[/]");
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Start Time");
        table.AddColumn("End Time");
        table.AddColumn("Title");
        table.AddColumn("Link");

        var sortedEvents = _currentEvents.OrderBy(e => e.StartTime).ToList();

        foreach (var evt in sortedEvents)
        {
            // Convert UTC times to local for display
            var startTime = evt.StartTime.ToLocalTime().ToString("ddd MMM dd HH:mm");
            var endTime = evt.EndTime.ToLocalTime().ToString("HH:mm");
            var title = Markup.Escape(evt.Title.Length > 40 ? evt.Title[..37] + "..." : evt.Title);
            var linkIndicator = evt.Link != null ? $"[green]{GetLinkTypeName(evt.Link)}[/]" : "[grey]-[/]";

            table.AddRow(startTime, endTime, title, linkIndicator);
        }

        return new Rows(
            table,
            new Markup($"[green]Found {_currentEvents.Count} upcoming event(s).[/]"));
    }

    private static IRenderable BuildFooter()
    {
        return new Markup("[grey]Press Ctrl+C to exit...[/]");
    }

    private static string GetLinkTypeName(MeetingLink link) => link switch
    {
        GoogleMeetLink => "Google Meet",
        ZoomLink => "Zoom",
        MicrosoftTeamsLink => "Teams",
        OtherLink => "Link",
        _ => "Link"
    };
}
