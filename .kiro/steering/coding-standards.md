---
inclusion: auto
---

# Coding Standards

## General Guidelines

### Target Framework
- All projects target .NET 10 (`<TargetFramework>net10.0</TargetFramework>`)
- Use modern C# features (records, pattern matching, implicit usings)

### Nullability
- Nullable reference types are enabled in all projects
- Avoid nullable types where possible; use Result pattern to represent absence of values
- Use `?` for nullable types only when truly optional (e.g., optional configuration parameters)
- Use Assert methods for non-null validation
- Prefer returning `Result<T, Error>` over returning `T?` when a value may not exist

### Immutability
- Prefer immutable types (records, init-only properties)
- Use `IReadOnlyList<T>` for collections in public APIs
- Domain events MUST be immutable (use records)

## Naming Conventions

### Folders
- Use pluralized names: `Meetings/`, `Notifications/`, `Errors/`, `Events/`
- Prevents naming collisions with type names
- Groups related functionality together

### Namespaces
- Match folder structure: `MeetingReminder.Domain.Meetings`
- Use pluralized namespace segments matching folder names

### Types
- Use singular names: `MeetingEvent`, `NotificationLevel`, `CalendarError`
- Records for immutable data: `DomainEvent`, `Error`, `MeetingLink`
- Classes for entities with behavior: `MeetingEvent`, `MeetingState`
- Use case classes: verb-phrases (e.g., `FetchCalendarEvents`, `CalculateNotificationLevel`)

## Code Organization

### No Region Directives
- **NEVER use `#region` / `#endregion` directives in any code**
- Region directives hide code complexity and make navigation harder
- Use proper class organization and smaller classes instead
- Group related tests using nested classes with descriptive names (e.g., `ConstructorValidation`, `ValidationTests`)
- Nested test classes should inherit from the parent test fixture to share setup and helper methods

### Using Statements
```csharp
// Static usings for Assert at the top
using static MeetingReminder.Domain.Assert;

// Then regular usings
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Domain.Meetings;
```

### Domain Entities
```csharp
public class MeetingEvent
{
    // Properties with init-only setters
    public string Id { get; init; }
    public string Title { get; init; }
    
    // Constructor with validation
    public MeetingEvent(string id, string title)
    {
        Id = NotNullOrEmpty(id);
        Title = NotNullOrEmpty(title);
    }
    
    // Domain logic methods
    public TimeSpan GetTimeUntilStart(DateTime currentTime)
        => StartTime - currentTime;
}
```

### Value Objects
```csharp
// Use abstract records with sealed subclasses for type hierarchies
public abstract record MeetingLink
{
    public string Url { get; init; }
    public abstract bool IsVideoConferencing { get; }

    protected MeetingLink(string url)
    {
        Url = NotNullOrEmpty(url);
    }
}

public sealed record GoogleMeetLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => true;
}
```

### Domain Events
```csharp
// Inherit from abstract DomainEvent record
public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    IReadOnlyList<MeetingEvent> AddedEvents,
    IReadOnlyList<string> RemovedEventIds,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
```

### Error Types
```csharp
// Inherit from abstract Error record
public sealed record CalendarError(
    string Message,
    string? CalendarSource = null,
    Exception? InnerException = null) : Error(Message);
```

## Result Pattern Usage

### Returning Results
```csharp
// Success - implicit conversion
public Result<MeetingEvent, CalendarError> GetMeeting(string id)
{
    var meeting = FindMeeting(id);
    return meeting; // Implicit conversion from MeetingEvent
}

// Error - implicit conversion
public Result<MeetingEvent, CalendarError> GetMeeting(string id)
{
    if (string.IsNullOrEmpty(id))
        return new CalendarError("Meeting ID is required");
    
    // ...
}
```

### Consuming Results
```csharp
// Pattern matching
var result = GetMeeting(id);
result.Match(
    meeting => Console.WriteLine(meeting.Title),
    error => Console.WriteLine(error.Message));

// Monadic chaining
var title = GetMeeting(id)
    .Map(meeting => meeting.Title)
    .GetValueOrDefault("Unknown");
```

## Documentation

### XML Comments
- Add XML comments for public types and members
- Include `<summary>` for all public APIs
- Use `<param>` and `<returns>` for methods

```csharp
/// <summary>
/// Calculates the time remaining until the meeting starts.
/// Returns negative TimeSpan if the meeting has already started.
/// </summary>
/// <param name="currentTime">The current time to calculate from</param>
/// <returns>TimeSpan representing time until meeting start</returns>
public TimeSpan GetTimeUntilStart(DateTime currentTime)
    => StartTime - currentTime;
```

## Testing Guidelines

### Property-Based Tests
- Use FsCheck for property-based testing
- Minimum 100 iterations per property test
- Tag with comment: `// Feature: meeting-reminder-tui, Property {number}: {property_text}`

### Unit Tests
- Use NUnit with AwesomeAssertions for readable assertions
- Test specific examples and edge cases
- Focus on core functional logic only
- Create MINIMAL test solutions

## Error Handling

### Never Use Exceptions for Control Flow
- Use Result pattern instead
- Exceptions only for truly exceptional cases
- All operations return Result types

### Validation
- Use Assert methods in constructors
- Validate at domain boundaries
- Return CalendarError, NotificationError, or ConfigurationError as appropriate

## Performance Considerations

### Thread Safety
- Use immutable types for cross-thread communication
- Use `BlockingCollection<T>` for event bus
- Use `ConcurrentDictionary` for shared state
- Use `SemaphoreSlim` to prevent overlapping operations

### Collections
- Use `IReadOnlyList<T>` for public APIs
- Use `List<T>` internally, expose as `AsReadOnly()`
- Avoid LINQ in hot paths if performance critical

## DateTime and Time Zone Handling

### UTC Throughout the Stack
- **All DateTime values in Domain, Application, and Infrastructure layers MUST use UTC**
- Store and process all times as UTC internally
- Convert to local time ONLY in the UI layer when displaying to users
- Use `DateTime.UtcNow` or `ITimeProvider.UtcNow`, never `DateTime.Now` in non-UI code

### DateTime Kind
- Always specify `DateTimeKind.Utc` when creating DateTime values
- When parsing external data (iCal, APIs), convert to UTC immediately
- Use `.ToUniversalTime()` when receiving local times from external sources

### ITimeProvider
- Use `ITimeProvider.UtcNow` for testability
- Never use `ITimeProvider.Now` in Domain, Application, or Infrastructure layers
- `ITimeProvider.Now` is only for UI display purposes

### UI Layer Conversion
```csharp
// In UI/Presentation layer only:
var localTime = utcDateTime.ToLocalTime();
var displayString = localTime.ToString("ddd MMM dd HH:mm");
```

### Domain Events and Messages
```csharp
// Always use UTC for timestamps in domain events
public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    DateTime OccurredAt) // OccurredAt is always UTC
```

### Testing
- Use `DateTimeKind.Utc` in test fixtures
- FakeTimeProvider should return UTC times
