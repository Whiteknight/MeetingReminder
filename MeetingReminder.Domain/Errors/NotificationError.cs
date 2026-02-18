namespace MeetingReminder.Domain.Errors;

public sealed record NotificationError(
    string Message,
    string? StrategyName = null) : Error(Message);
