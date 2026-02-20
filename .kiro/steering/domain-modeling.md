---
inclusion: auto
---

# Domain Modeling Guidelines

## Vertical Slice Organization

### Current Domain Slices

**Calendars Slice** (`MeetingReminder.Domain.Calendars`)
- Interfaces: `ICalendarSource`
- DTOs: `RawCalendarEvent`
- Events: `CalendarEventsUpdated`
- Errors: `CalendarError`

**Meetings Slice** (`MeetingReminder.Domain.Meetings`)
- Entities: `MeetingEvent`, `MeetingState`
- Value Objects: `MeetingLink` (abstract), `GoogleMeetLink`, `ZoomLink`, `MicrosoftTeamsLink`, `OtherLink`
- Events: `MeetingAcknowledged`
- Errors: `MeetingLinkError`

**Notifications Slice** (`MeetingReminder.Domain.Notifications`)
- Enums: `NotificationLevel`
- Events: `NotificationStateChanged`
- Errors: `NotificationError`

**Configuration Slice** (`MeetingReminder.Domain.Configuration`)
- Interfaces: `IConfigurationManager`, `IAppConfiguration`, `INotificationThresholds`, etc.
- Errors: `ConfigurationError`

### Adding New Domain Concepts

When adding new domain concepts, follow this decision tree:

1. **Identify the domain area**: Which slice does this belong to?
   - Meeting-related? → `Meetings/`
   - Notification-related? → `Notifications/`
   - New area? → Create new pluralized folder

2. **Choose the right type**:
   - Has identity and lifecycle? → Entity (class)
   - Immutable data with no identity? → Value Object (record)
   - Cross-thread message? → Domain Event (record inheriting DomainEvent)
   - Error condition? → Error (record inheriting Error)
   - Set of named constants? → Enum

3. **Place in the correct folder**:
   - All related types go in the same slice folder
   - Don't separate by technical concern

## Entity Guidelines

### When to Use Entities
- Has a unique identifier
- Has a lifecycle (created, modified, deleted)
- Has behavior that changes state
- Identity matters more than attributes

### Entity Pattern
```csharp
public class MeetingState
{
    // Identity
    public MeetingEvent Event { get; init; }
    
    // State
    public NotificationLevel CurrentLevel { get; private set; }
    public bool IsAcknowledged { get; private set; }
    
    // Constructor with validation
    public MeetingState(MeetingEvent meetingEvent)
    {
        Event = NotNull(meetingEvent);
        CurrentLevel = NotificationLevel.None;
        IsAcknowledged = false;
    }
    
    // Domain logic that changes state
    public void UpdateNotificationLevel(NotificationLevel level)
    {
        if (level > CurrentLevel)
            CurrentLevel = level;
    }
    
    public void Acknowledge()
    {
        IsAcknowledged = true;
        CurrentLevel = NotificationLevel.None;
    }
}
```

### Entity Rules
- Use classes, not records
- Private setters for mutable state
- Public methods for state changes
- Validate in constructor
- Keep domain logic in the entity

## Value Object Guidelines

### When to Use Value Objects
- No unique identifier
- Immutable
- Equality based on attributes, not identity
- Can be freely replaced

### Value Object Pattern
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

public sealed record ZoomLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => true;
}

public sealed record OtherLink(string Url) : MeetingLink(Url)
{
    public override bool IsVideoConferencing => false;
}
```

### Value Object Rules
- Use records for automatic value equality
- Validate in constructor
- All properties are init-only
- No mutable state
- No identity

## Domain Event Guidelines

### When to Use Domain Events
- Something significant happened in the domain
- Other parts of the system need to react
- Cross-thread communication
- Audit trail or event sourcing

### Domain Event Pattern
```csharp
public record CalendarEventsUpdated(
    IReadOnlyList<MeetingEvent> AllEvents,
    IReadOnlyList<MeetingEvent> AddedEvents,
    IReadOnlyList<string> RemovedEventIds,
    DateTime OccurredAt) : DomainEvent(OccurredAt);
```

### Domain Event Rules
- Inherit from `DomainEvent` abstract record
- Always include `OccurredAt` timestamp
- Use past tense names (CalendarEventsUpdated, MeetingAcknowledged)
- Immutable (records provide this)
- Include all relevant data (no lazy loading)
- Place in the domain slice where the event originates

## Error Type Guidelines

### When to Use Domain Errors
- Domain-specific error conditions
- Need structured error information
- Want to avoid exceptions

### Error Pattern
```csharp
public sealed record CalendarError(
    string Message,
    string? CalendarSource = null,
    Exception? InnerException = null) : Error(Message);
```

### Error Rules
- Inherit from `Error` abstract record
- Use sealed to prevent further inheritance
- Include context via optional parameters
- Place errors in the same folder as the domain types they relate to
- Use descriptive names: CalendarError, NotificationError, ConfigurationError, MeetingLinkError

## Domain Logic Placement

### Entity Methods
```csharp
public class MeetingEvent
{
    // Domain logic belongs in the entity
    public TimeSpan GetTimeUntilStart(DateTime currentTime)
        => StartTime - currentTime;
    
    public bool IsInProgress(DateTime currentTime)
        => currentTime >= StartTime && currentTime < EndTime;
}
```

### When to Use Domain Services
- Logic spans multiple entities
- Logic doesn't naturally belong to one entity
- Stateless operations

Example: `CalculateNotificationLevelHandler` - spans MeetingEvent, NotificationThresholds, and CalendarNotificationRules

## Validation Strategy

### Constructor Validation
```csharp
public MeetingEvent(string id, string title, DateTime startTime, DateTime endTime)
{
    Id = NotNullOrEmpty(id);
    Title = NotNullOrEmpty(title);
    StartTime = startTime;
    EndTime = endTime;
    
    if (EndTime < StartTime)
        throw new ArgumentException("End time must be after start time");
}
```

### Domain Invariants
- Enforce in constructor
- Enforce in state-changing methods
- Use Assert methods for null checks
- Throw ArgumentException for business rule violations in constructors
- Return Result<T, Error> for business rule violations in methods

## Immutability Guidelines

### Prefer Immutability
- Domain events: Always immutable (records)
- Value objects: Always immutable (records)
- Entities: Mutable state via methods, but properties are init-only
- Collections: Use `IReadOnlyList<T>` in public APIs

### When Mutability is Acceptable
- Entity state that changes through domain methods
- Internal collections that are exposed as read-only
- Caching or performance optimization (document why)

## Naming Conventions

### Entities
- Singular nouns: `MeetingEvent`, `MeetingState`
- Descriptive of what they represent
- Avoid generic names like `Data`, `Info`, `Manager`

### Value Objects
- Singular nouns: `MeetingLink`, `NotificationThresholds`
- Often compound nouns describing the value

### Domain Events
- Past tense: `CalendarEventsUpdated`, `MeetingAcknowledged`, `NotificationStateChanged`
- Describes what happened, not what should happen

### Enums
- Singular noun: `NotificationLevel`, `CalendarType`
- Values are singular: `NotificationLevel.Gentle`, not `NotificationLevel.Gentles`
- Prefer subclassed records over enums when behavior differs by type (e.g., `MeetingLink` hierarchy)
