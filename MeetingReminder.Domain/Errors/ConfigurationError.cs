namespace MeetingReminder.Domain.Errors;

public sealed record ConfigurationError(
    string Message,
    string? ConfigKey = null) : Error(Message);
