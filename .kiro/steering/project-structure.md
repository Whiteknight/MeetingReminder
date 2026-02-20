---
inclusion: auto
---

# Project Structure Guide

## Solution Organization

The solution uses Solution Folders to organize projects:

```
MeetingReminder.sln
├── MeetingReminder.Domain/           # Core domain (no dependencies)
├── MeetingReminder.Application/      # Use cases (depends on Domain)
├── MeetingReminder.Infrastructure/   # Cross-cutting infrastructure
├── MeetingReminder.GoogleCalendar/   # Google Calendar integration
├── MeetingReminder.ConsoleTui/       # Spectre.Console TUI
├── Tests/                            # Solution Folder
│   ├── MeetingReminder.Domain.Tests/
│   ├── MeetingReminder.Application.Tests/
│   ├── MeetingReminder.Infrastructure.Tests/
│   └── MeetingReminder.ManualTests/  # Manual testing console app
└── Platforms/                        # Solution Folder
    ├── MeetingReminder.Infrastructure.Windows/
    └── MeetingReminder.Infrastructure.Linux/
```

### Solution Folder Rules

1. **Tests/** - All test projects (unit tests, integration tests, manual tests) go in this folder
2. **Platforms/** - Platform-specific infrastructure projects go in this folder
3. When adding a new test project, add it to the Tests/ solution folder
4. When adding a new platform-specific project, add it to the Platforms/ solution folder

## Domain Project Structure

```
MeetingReminder.Domain/
├── Calendars/                        # Calendar domain slice
│   ├── ICalendarSource.cs           # Interface
│   ├── RawCalendarEvent.cs          # DTO
│   ├── CalendarEventsUpdated.cs     # Domain event
│   └── CalendarError.cs             # Error type
├── Meetings/                         # Meeting domain slice
│   ├── MeetingEvent.cs              # Entity
│   ├── MeetingState.cs              # Entity
│   ├── MeetingLink.cs               # Abstract value object + subclasses
│   ├── MeetingAcknowledged.cs       # Domain event
│   └── MeetingLinkError.cs          # Error type
├── Notifications/                    # Notification domain slice
│   ├── NotificationLevel.cs         # Enum
│   ├── NotificationStateChanged.cs  # Domain event
│   └── NotificationError.cs         # Error type
├── Configuration/                    # Configuration domain slice
│   ├── IConfigurationManager.cs     # Interface + related interfaces
│   └── ConfigurationError.cs        # Error type
├── Assert.cs                         # Guard clauses
├── Error.cs                          # Abstract base for errors
└── Result.cs                         # Result pattern implementation
```

### Domain Organization Rules

1. **Vertical Slices**: Group by domain area (Meetings/, Notifications/)
2. **Pluralized Folders**: Use plural names to avoid naming collisions
3. **Co-location**: Keep entities, value objects, and events together in their slice
4. **Cross-cutting Concerns**: Errors/ folder for domain errors used across slices
5. **Base Types**: Root-level files for abstract base types (DomainEvent, Error, Result)

## Application Project Structure

```
MeetingReminder.Application/
├── UseCases/                         # Organized by use case
│   ├── FetchCalendarEvents/
│   │   ├── FetchCalendarEventsQuery.cs
│   │   └── FetchCalendarEvents.cs    # Use case class (verb-phrase name)
│   ├── CalculateNotificationLevel/
│   │   ├── CalculateNotificationLevelQuery.cs
│   │   └── CalculateNotificationLevel.cs
│   ├── AcknowledgeMeeting/
│   │   ├── AcknowledgeMeetingCommand.cs
│   │   └── AcknowledgeMeeting.cs
│   └── ExtractMeetingLink/
│       ├── ExtractMeetingLinkQuery.cs
│       └── ExtractMeetingLink.cs
├── Abstractions/                     # Interfaces for infrastructure
│   ├── ICalendarSource.cs
│   ├── INotificationStrategy.cs
│   ├── IBrowserLauncher.cs
│   ├── IMeetingRepository.cs
│   └── IEventBus.cs
└── Common/                           # Shared application types
    └── Unit.cs                       # Unit type for void results
```

### Application Organization Rules

1. **Use Case Folders**: Each use case in its own folder
2. **Command/Query**: Separate commands (write) from queries (read)
3. **Use Case Classes**: Named as verb-phrases (e.g., `FetchCalendarEvents`, not `FetchCalendarEventsHandler`)
4. **Abstractions**: Interfaces for infrastructure dependencies
5. **No Domain Logic**: Application layer orchestrates, doesn't contain business rules

## Infrastructure Project Structure

```
MeetingReminder.Infrastructure/
├── Configuration/
│   ├── AppConfiguration.cs
│   ├── NotificationThresholds.cs
│   ├── CalendarConfiguration.cs
│   └── ConfigurationManager.cs
├── EventBus/
│   └── InMemoryEventBus.cs
├── Logging/
│   └── (logging infrastructure)
└── Threading/
    ├── CalendarPollingService.cs
    └── NotificationProcessingService.cs
```

## Vendor Library Projects

### GoogleCalendar Project
```
MeetingReminder.GoogleCalendar/
├── GoogleCalendarSource.cs          # Implements ICalendarSource
└── GoogleCalendarAuthenticator.cs
```

### ICal Project
```
MeetingReminder.ICal/
└── ICalSource.cs                    # Implements ICalendarSource
```

### Notifications Project
```
MeetingReminder.Notifications/
├── Beep/
│   └── BeepNotificationStrategy.cs
├── Flash/
│   └── TerminalFlashStrategy.cs
├── Sound/
│   └── SoundFileStrategy.cs
└── SystemNotification/
    └── SystemNotificationStrategy.cs
```

## Console TUI Project

```
MeetingReminder.ConsoleTui/
├── UI/
│   ├── IUserInterface.cs
│   └── SpectreConsoleTUI.cs
├── Program.cs                        # Entry point, DI setup
└── appsettings.json                  # Configuration
```

## File Naming Conventions

### Domain Layer
- Entities: `MeetingEvent.cs`, `MeetingState.cs`
- Value Objects: `MeetingLink.cs`, `NotificationThresholds.cs`
- Events: `CalendarEventsUpdated.cs`, `MeetingAcknowledged.cs`
- Errors: `CalendarError.cs`, `NotificationError.cs`
- Enums: `NotificationLevel.cs`, `MeetingLinkType.cs`

### Application Layer
- Commands: `AcknowledgeMeetingCommand.cs`
- Queries: `FetchCalendarEventsQuery.cs`
- Use Cases: `FetchCalendarEvents.cs`, `CalculateNotificationLevel.cs` (verb-phrase names)
- Interfaces: `ICalendarSource.cs`, `IEventBus.cs`

### Infrastructure Layer
- Services: `CalendarPollingService.cs`, `NotificationProcessingService.cs`
- Implementations: `InMemoryEventBus.cs`, `ConfigurationManager.cs`

## Dependency Flow

```
ConsoleTui → Infrastructure → Application → Domain
         ↓
    GoogleCalendar → Application → Domain
         ↓
    Infrastructure.Windows → Infrastructure → Domain
         ↓
    Infrastructure.Linux → Infrastructure → Domain
```

## Platform-Specific Projects

Platform-specific projects are organized in the Platforms/ solution folder:

```
Platforms/                            # Solution Folder
├── MeetingReminder.Infrastructure.Windows/
│   └── Notifications/
│       ├── BeepNotificationStrategy.cs
│       ├── NotificationProvider.cs
│       ├── SoundPlayer.cs
│       └── TerminalFlashStrategy.cs
└── MeetingReminder.Infrastructure.Linux/
    └── Notifications/
        ├── NotificationProvider.cs
        ├── SoundPlayer.cs
        └── TerminalFlashStrategy.cs
```

### Platform Project Rules

1. **Naming**: `MeetingReminder.Infrastructure.{Platform}`
2. **Target Framework**: 
   - Windows: `net10.0-windows10.0.17763.0` (for UWP APIs)
   - Linux: `net10.0`
3. **Class Naming**: Classes don't need platform prefix (namespace provides context)
   - Use `NotificationProvider`, not `WindowsNotificationProvider`
4. **Dependencies**: Reference `MeetingReminder.Infrastructure` for shared interfaces
5. **Platform-specific packages**: Add platform-specific NuGet packages here
   - Windows: `Microsoft.Toolkit.Uwp.Notifications`
6. **Conditional References**: ConsoleTui conditionally references based on build platform

### Dependency Rules
1. Domain has NO dependencies on other projects
2. Application depends ONLY on Domain
3. Infrastructure depends on Application and Domain
4. Vendor libraries depend on Application and Domain
5. ConsoleTui depends on all layers (composition root)

## Adding New Features

### New Domain Concept
1. Identify the domain slice (Meetings/, Notifications/, or new slice)
2. Create the type in the appropriate slice folder
3. Update namespace to match folder structure
4. Add related events to the same slice

### New Use Case
1. Create folder in `Application/UseCases/`
2. Add Command/Query record
3. Add Handler class
4. Define any new abstractions in `Application/Abstractions/`
5. Implement abstractions in Infrastructure or vendor libraries

### New Vendor Integration
1. Create new project: `MeetingReminder.{VendorName}/`
2. Reference Application and Domain projects
3. Implement interfaces from Application/Abstractions/
4. Add NuGet packages for vendor SDK
5. Register in ConsoleTui DI container

## Configuration Files

### Project Files (.csproj)
- Target Framework: `<TargetFramework>net10.0</TargetFramework>`
- Nullable: `<Nullable>enable</Nullable>`
- Implicit Usings: `<ImplicitUsings>enable</ImplicitUsings>`

### Solution File
- Keep all projects in solution
- Maintain project dependencies correctly

## Testing Structure

Test projects are organized in the Tests/ solution folder:

```
Tests/                                # Solution Folder
├── MeetingReminder.Domain.Tests/     # Domain unit tests
│   ├── Meetings/
│   │   ├── MeetingEventTests.cs
│   │   └── MeetingStateTests.cs
│   └── ResultTests.cs
├── MeetingReminder.Application.Tests/ # Application unit tests
│   └── UseCases/
│       ├── CalculateNotificationLevelTests.cs
│       └── ExtractMeetingLinkTests.cs
├── MeetingReminder.Infrastructure.Tests/ # Infrastructure unit tests
│   ├── Configuration/
│   ├── ICal/
│   ├── Notifications/
│   └── Threading/
└── MeetingReminder.ManualTests/      # Manual testing console app
    └── Program.cs                    # Interactive notification tests
```

### Test Project Naming
- Unit test projects: `{ProjectName}.Tests`
- Manual test projects: `MeetingReminder.ManualTests`
- Integration test projects: `{ProjectName}.IntegrationTests`

### Test Frameworks
- Use NUnit for test framework
- Use AwesomeAssertions for fluent assertions
- Use NSubstitute for mocking

## Build Output

- All projects output to `bin/Debug/net10.0/` or `bin/Release/net10.0/`
- ConsoleTui produces executable: `MeetingReminder.ConsoleTui.exe`
- Other projects produce DLLs

## Documentation Location

- Architecture docs: `.kiro/specs/meeting-reminder-tui/`
- Steering files: `.kiro/steering/`
- README: Root level `README.md`
- Implementation notes: `.implementation-notes.md`
