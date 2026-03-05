using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace MeetingReminder.ConsoleTui;

public static class InterfaceBuilder
{
    public static IRenderable BuildDisplay(IReadOnlyList<MeetingState> meetings, int maxRows, int selectedMeetingIndex)
        => new Rows(
            BuildMeetingsPanel(meetings, maxRows, selectedMeetingIndex),
            BuildKeyboardHints());

    private static IRenderable BuildMeetingsPanel(IReadOnlyList<MeetingState> meetings, int maxRows, int selectedMeetingIndex)
        => new Panel(BuildEventsTable(meetings, maxRows, selectedMeetingIndex))
            .Header($"[yellow]{DateTime.UtcNow:ddd MMM dd} Meetings[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.DarkSlateGray1)
            .Expand();

    private static IRenderable BuildEventsTable(IReadOnlyList<MeetingState> meetings, int maxRows, int selectedMeetingIndex)
    {
        var table = CreateEventsTableStructure();
        if (meetings.Count == 0)
        {
            PadBlankRows(table, maxRows);
            return new Rows(
                table,
                new Markup("[yellow]No upcoming meetings in the next 7 days.[/]"));
        }

        if (selectedMeetingIndex < 0)
            selectedMeetingIndex = 0;

        var visibleMeetings = meetings.Take(maxRows).ToList();
        var selectedIndex = selectedMeetingIndex >= 0 && selectedMeetingIndex < meetings.Count ? selectedMeetingIndex : 0;

        for (int i = 0; i < visibleMeetings.Count; i++)
        {
            var meeting = visibleMeetings[i];
            var isSelected = i == selectedIndex;
            AddEventRow(table, meeting, isSelected);
        }

        PadBlankRows(table, maxRows - visibleMeetings.Count);

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
        var start = meeting.Event.StartTime.ToLocalTime().ToString("HH:mm");
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
            "[grey]Enter/Spacebar[/] Acknowledge  " +
            "[grey]O[/] Open link  " +
            "[grey]Up/Down[/] Navigate  " +
            "[grey]Ctrl+C[/] Exit");
}
