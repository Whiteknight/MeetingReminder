namespace MeetingReminder.Domain;

/// <summary>
/// Abstract base record for all domain events.
/// Provides default immutability and low-boilerplate event definitions.
/// All domain events must include the timestamp when they occurred.
/// </summary>
public abstract record DomainEvent(DateTime OccurredAt);
