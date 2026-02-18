namespace MeetingReminder.Domain.Errors;

public sealed record CalendarError(
    string Message,
    string? CalendarSource = null,
    Exception? InnerException = null) : Error(Message);
