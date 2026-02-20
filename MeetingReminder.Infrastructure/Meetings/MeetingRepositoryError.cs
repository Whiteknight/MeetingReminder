using MeetingReminder.Domain;

namespace MeetingReminder.Infrastructure.Meetings;

/// <summary>
/// Error type for meeting repository operations.
/// </summary>
public sealed record MeetingRepositoryError(string Message) : Error(Message);
