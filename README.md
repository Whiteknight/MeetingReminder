# Meeting Reminder

A C# application that provides aggressive, escalating notifications for calendar meetings following Clean Architecture principles. Designed to support multiple frontends (TUI, GUI, etc.).

## Project Structure

The solution follows Clean Architecture with vertical slice organization:

### Core Projects

- **MeetingReminder.Domain** - Core domain entities, value objects, and business logic (no dependencies)
- **MeetingReminder.Application** - Use cases organized as vertical slices (depends only on Domain)
- **MeetingReminder.Infrastructure** - Cross-cutting infrastructure (configuration, logging, threading)

### Vendor-Specific Projects

- **MeetingReminder.GoogleCalendar** - Google Calendar API integration
- **MeetingReminder.ICal** - iCal calendar parsing and integration
- **MeetingReminder.Notifications** - Notification strategy implementations

### Presentation

- **MeetingReminder.ConsoleTui** - Spectre.Console-based terminal user interface (first frontend implementation)

## Dependencies

The project uses the following NuGet packages:

- **Spectre.Console** (v0.54.0) - Terminal UI framework
- **Ical.Net** (v5.2.1) - iCal parsing library
- **Google.Apis.Calendar.v3** (v1.73.0.3993) - Google Calendar API client
- **System.Text.Json** (v10.0.3) - JSON configuration management

## Building

```bash
dotnet build MeetingReminder.sln
```

## Running

```bash
dotnet run --project MeetingReminder.ConsoleTui
```

## Architecture Principles

- **Clean Architecture**: Dependencies point inward; domain has no external dependencies
- **Vertical Slices**: Features organized by use case rather than technical layer
- **Result Pattern**: All operations return `Result<T>` instead of throwing exceptions
- **Thread Safety**: Polling and notification on separate threads with pub/sub communication
- **Modularity**: Vendor-specific logic in separate projects for easy swapping
