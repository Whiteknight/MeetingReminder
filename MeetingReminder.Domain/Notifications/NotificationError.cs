namespace MeetingReminder.Domain.Notifications;

public sealed record NotificationError(
    string Message,
    string? StrategyName = null) : Error(Message);
