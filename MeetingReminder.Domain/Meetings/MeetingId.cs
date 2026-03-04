namespace MeetingReminder.Domain.Meetings;

public readonly record struct MeetingId(string Calendar, string Id)
{
    public bool IsValid => !string.IsNullOrEmpty(Calendar) && !string.IsNullOrEmpty(Id);
}
