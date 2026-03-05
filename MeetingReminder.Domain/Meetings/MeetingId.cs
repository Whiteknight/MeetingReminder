using MeetingReminder.Domain.Calendars;

namespace MeetingReminder.Domain.Meetings;

public readonly record struct MeetingId(CalendarName Calendar, string Id)
{
    public MeetingId(string calendar, string id)
        : this(new CalendarName(calendar), id)
    {
    }

    public bool IsValid => Calendar.IsValid && !string.IsNullOrEmpty(Id);
}
