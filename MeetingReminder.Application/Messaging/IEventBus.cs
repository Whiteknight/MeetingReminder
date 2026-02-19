using MeetingReminder.Domain;

namespace MeetingReminder.Application.Abstractions;

/// <summary>
/// Defines a pub/sub event bus for domain events.
/// Enables thread-safe communication between different parts of the application
/// using the observer pattern with domain events.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes a domain event to all subscribers.
    /// This method is thread-safe and non-blocking.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event to publish</typeparam>
    /// <param name="event">The event instance to publish</param>
    void Publish<TEvent>(TEvent @event) where TEvent : DomainEvent;

    /// <summary>
    /// Subscribes to domain events of a specific type.
    /// The handler will be invoked whenever an event of type TEvent is published.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event to subscribe to</typeparam>
    /// <param name="handler">The action to invoke when an event is published</param>
    /// <returns>A disposable subscription that can be disposed to unsubscribe</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : DomainEvent;
}
