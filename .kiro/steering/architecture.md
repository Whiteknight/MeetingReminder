---
inclusion: auto
---

# Architecture Guidelines

## Project Overview

This is a C# .NET 10 console application following Clean Architecture principles with vertical slice organization. The Meeting Reminder TUI provides aggressive, escalating notifications for calendar meetings.

## Architectural Principles

### Clean Architecture Layers

1. **Domain Layer** (`MeetingReminder.Domain`)
   - No external dependencies
   - Contains core business entities, value objects, and domain logic
   - Organized by vertical slices (Meetings/, Notifications/)
   - All types are in their respective domain folders

2. **Application Layer** (`MeetingReminder.Application`)
   - Depends only on Domain
   - Use cases organized as vertical slices
   - Each use case has: Command/Query, Handler, Validator
   - Returns `Result<T>` or `Result<T, TError>` from all operations

3. **Infrastructure Layer** (`MeetingReminder.Infrastructure`)
   - Implements application abstractions
   - Configuration management, event bus, logging
   - No business logic

4. **Vendor-Specific Libraries** (separate projects)
   - GoogleCalendar, ICal, Notifications
   - Each implements interfaces from Application layer
   - Can be swapped or extended independently

### Vertical Slice Organization

**DO:**
- Group related functionality by domain area (Meetings/, Notifications/)
- Use pluralized folder names to avoid naming collisions
- Keep entities, value objects, and events together in their domain slice
- Example: `MeetingReminder.Domain.Meetings` contains MeetingEvent, MeetingState, MeetingLink, CalendarEventsUpdated, MeetingAcknowledged

**DON'T:**
- Separate by technical concerns (Entities/, ValueObjects/, Events/)
- Use singular folder names that match type names
- Scatter related domain concepts across multiple folders

## Key Design Patterns

### Result Pattern

All operations return `Result<T, TError>` instead of throwing exceptions.

```csharp
public Result<MeetingEvent, CalendarError> GetMeeting(string id)
{
    if (string.IsNullOrEmpty(id))
        return new CalendarError("Meeting ID is required");
    
    var meeting = FindMeeting(id);
    return meeting; // Implicit conversion
}
```

### Abstract Record Pattern

Use abstract records for base types (Error, DomainEvent) to provide:
- Default immutability
- Low boilerplate
- Thread-safe cross-thread messaging
- Value equality

```csharp
public abstract record DomainEvent(DateTime OccurredAt);

public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    IReadOnlyList<MeetingEvent> AddedEvents,
    IReadOnlyList<string> RemovedEventIds,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
```

### Assert Pattern

Use defensive guard clauses with automatic parameter name capture:

```csharp
using static MeetingReminder.Domain.Assert;

public MeetingEvent(string id, string title)
{
    Id = NotNullOrEmpty(id);
    Title = NotNullOrEmpty(title);
}
```

## Threading Model

- **Main Thread**: Spectre.Console TUI, handles user input
- **Calendar Polling Thread**: Polls calendar sources, publishes events
- **Notification Thread**: Calculates levels, executes strategies

Communication via event bus with `BlockingCollection<T>` for thread-safe pub/sub.

## Technology Stack

- .NET 10
- Spectre.Console for TUI
- Google.Apis.Calendar.v3 for Google Calendar
- Ical.Net for iCal parsing
- System.Text.Json for configuration
- FsCheck for property-based testing (when implemented)

## Dependency Rules

- Domain has NO external dependencies
- Application depends only on Domain
- Infrastructure depends on Application and Domain
- Vendor libraries depend on Application and Domain
- Console app depends on all layers

Dependencies always point inward toward the Domain.
