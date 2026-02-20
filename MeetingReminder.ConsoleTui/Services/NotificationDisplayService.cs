using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Threading.Channels;

namespace MeetingReminder.ConsoleTui.Services;

/// <summary>
/// Hosted service that reads notification state changes from the channel
/// and displays them in the console using Spectre.Console.
/// </summary>
public class NotificationDisplayService : BackgroundService
{
    private readonly ChannelReader<NotificationStateChanged> _channelReader;
    private readonly ILogger<NotificationDisplayService> _logger;

    public NotificationDisplayService(
        ChannelReader<NotificationStateChanged> channelReader,
        ILogger<NotificationDisplayService> logger)
    {
        _channelReader = channelReader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification display service started");

        await foreach (var notification in _channelReader.ReadAllAsync(stoppingToken))
        {
            DisplayNotifications(notification);
        }
    }

    private void DisplayNotifications(NotificationStateChanged notification)
    {
        foreach (var state in notification.ActiveNotifications)
        {
            DisplayMeetingNotification(state);
        }
    }

    private void DisplayMeetingNotification(MeetingState state)
    {
        var (color, emoji) = GetLevelStyle(state.CurrentLevel);
        var timeUntilStart = state.Event.StartTime - DateTime.UtcNow;
        var timeText = FormatTimeUntilStart(timeUntilStart);

        AnsiConsole.MarkupLine($"[{color}]{emoji} {Markup.Escape(state.Event.Title)}[/] - {timeText}");
    }

    private static (string color, string emoji) GetLevelStyle(NotificationLevel level)
    {
        return level switch
        {
            NotificationLevel.Gentle => ("blue", "📅"),
            NotificationLevel.Moderate => ("yellow", "⏰"),
            NotificationLevel.Urgent => ("orange1", "⚠️"),
            NotificationLevel.Critical => ("red", "🚨"),
            _ => ("grey", "📅")
        };
    }

    private static string FormatTimeUntilStart(TimeSpan timeSpan)
    {
        if (timeSpan <= TimeSpan.Zero)
            return "[red]NOW![/]";

        if (timeSpan.TotalMinutes < 1)
            return $"[red]in {timeSpan.Seconds}s[/]";

        if (timeSpan.TotalMinutes < 60)
            return $"in {(int)timeSpan.TotalMinutes}m";

        return $"in {(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
    }
}
