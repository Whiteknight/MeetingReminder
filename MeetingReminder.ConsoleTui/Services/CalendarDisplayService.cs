using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Hosted service that reads calendar events from the channel and updates the TUI display.
/// Uses Spectre.Console Live display for flicker-free updates.
/// Shows a countdown to the next poll with a spinner that updates every 5 seconds.
/// </summary>
public class CalendarDisplayService : BackgroundService
{
    private static readonly string[] _spinnerFrames = ["|", "/", "-", "\\"];
    private readonly IMeetingRepository _meetings;
    private readonly ILogger<CalendarDisplayService> _logger;
    private readonly TimeSpan _pollingInterval;

    private DateTime _lastPollTime = DateTime.UtcNow;
    private int _spinnerIndex;

    public CalendarDisplayService(
        IAppConfiguration configuration,
        IMeetingRepository meetings,
        ILogger<CalendarDisplayService> logger)
    {
        _pollingInterval = configuration.PollingInterval;
        _meetings = meetings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Calendar display service started");
        var initial = GetUpcoming();

        await AnsiConsole.Live(BuildDisplay(initial))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // Update countdown every 5 seconds
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    _spinnerIndex = (_spinnerIndex + 1) % _spinnerFrames.Length;

                    var upcoming = GetUpcoming();
                    // TODO: Get the current meetings from the repository and feed them in.
                    ctx.UpdateTarget(BuildDisplay(upcoming));
                }
            });
    }

    private IReadOnlyList<MeetingState> GetUpcoming()
    {
        return _meetings.GetAll().Match(m => m, _ => []);
    }

    private IRenderable BuildDisplay(IReadOnlyList<MeetingState> upcoming)
    {
        var rows = new Rows(
            BuildHeader(),
            new Text(""),
            BuildEventsTable(upcoming),
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

        var spinner = _spinnerFrames[_spinnerIndex];
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

    private IRenderable BuildEventsTable(IReadOnlyList<MeetingState> upcoming)
    {
        if (upcoming.Count == 0)
        {
            return new Markup("[yellow]No upcoming meetings.[/]");
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Start Time");
        table.AddColumn("End Time");
        table.AddColumn("Title");
        table.AddColumn("Link");

        var sortedEvents = upcoming.OrderBy(e => e.Event.StartTime).ToList();

        foreach (var evt in sortedEvents)
        {
            // Convert UTC times to local for display
            var startTime = evt.Event.StartTime.ToLocalTime().ToString("ddd MMM dd HH:mm");
            var endTime = evt.Event.EndTime.ToLocalTime().ToString("HH:mm");
            var title = Markup.Escape(evt.Event.Title.Length > 40 ? evt.Event.Title[..37] + "..." : evt.Event.Title);
            //var linkIndicator = evt.Event.Link != null ? $"[green]{GetLinkTypeName(evt.Link)}[/]" : "[grey]-[/]";
            var linkIndicator = "";
            table.AddRow(startTime, endTime, title, linkIndicator);
        }

        return new Rows(
            table,
            new Markup($"[green]Found {upcoming.Count} upcoming event(s).[/]"));
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
