---
inclusion: auto
fileMatchPattern: "**/*.cs"
---

# Result Pattern Usage Guide

## Overview

This project uses the Result pattern instead of exceptions for error handling. All operations that can fail return `Result<T, TError>` types.

## Why Result Pattern?

- Makes error handling explicit
- Forces callers to handle both success and failure cases
- Better for cross-thread communication
- Avoids expensive exception throwing
- Provides better type safety

## Basic Usage

### Returning Success
```csharp
public Result<MeetingEvent, CalendarError> GetMeeting(string id)
{
    var meeting = FindMeeting(id);
    return meeting; // Implicit conversion from MeetingEvent
}
```

### Returning Errors
```csharp
public Result<MeetingEvent, CalendarError> GetMeeting(string id)
{
    if (string.IsNullOrEmpty(id))
        return new CalendarError("Meeting ID is required");
    
    var meeting = FindMeeting(id);
    if (meeting == null)
        return new CalendarError("Meeting not found");
    
    return meeting;
}
```

## Consuming Results

### Pattern Matching
```csharp
var result = GetMeeting(id);
result.Match(
    meeting => Console.WriteLine($"Found: {meeting.Title}"),
    error => Console.WriteLine($"Error: {error.Message}"));
```

### Switch (for side effects)
```csharp
result.Switch(
    meeting => ProcessMeeting(meeting),
    error => LogError(error));
```

### Checking Success/Failure
```csharp
if (result.IsSuccess)
{
    var meeting = result.Match(m => m, _ => throw new InvalidOperationException());
    ProcessMeeting(meeting);
}
else if (result.IsError)
{
    var error = result.Match(_ => throw new InvalidOperationException(), e => e);
    LogError(error);
}
```

## Monadic Operations

### Map (Transform Success Value)
```csharp
Result<string, CalendarError> titleResult = GetMeeting(id)
    .Map(meeting => meeting.Title);
```

### Bind (Chain Operations)
```csharp
Result<MeetingLink?, CalendarError> linkResult = GetMeeting(id)
    .Bind(meeting => ExtractLink(meeting));
```

### MapError (Transform Error)
```csharp
Result<MeetingEvent, string> result = GetMeeting(id)
    .MapError(error => error.Message);
```

### And (Chain with Different Return Type)
```csharp
Result<bool, CalendarError> result = GetMeeting(id)
    .And(meeting => ValidateMeeting(meeting));
```

### Or (Fallback on Error)
```csharp
Result<MeetingEvent, NotificationError> result = GetMeeting(id)
    .Or(calendarError => GetMeetingFromCache(id));
```

## Side Effects

### OnSuccess
```csharp
var result = GetMeeting(id)
    .OnSuccess(meeting => Console.WriteLine($"Found: {meeting.Title}"));
// Returns the same result, allows chaining
```

### OnError
```csharp
var result = GetMeeting(id)
    .OnError(error => LogError(error));
// Returns the same result, allows chaining
```

## Utility Methods

### GetValueOrDefault
```csharp
var meeting = GetMeeting(id)
    .GetValueOrDefault(defaultMeeting);
```

### GetErrorOrDefault
```csharp
var error = GetMeeting(id)
    .GetErrorOrDefault(new CalendarError("Unknown error"));
```

### Is (Check for Specific Value)
```csharp
if (result.Is(expectedMeeting))
{
    // Result contains the expected meeting
}
```

## Common Patterns

### Aggregating Multiple Results
```csharp
public async Task<Result<IReadOnlyList<MeetingEvent>, CalendarError>> 
    FetchFromAllSources()
{
    var allEvents = new List<MeetingEvent>();
    var errors = new List<CalendarError>();
    
    foreach (var source in _sources)
    {
        var result = await source.FetchEventsAsync();
        
        if (result.IsSuccess)
            allEvents.AddRange(result.Match(events => events, _ => []));
        else
            errors.Add(result.Match(_ => null!, error => error));
    }
    
    // Succeed if at least one source worked
    if (allEvents.Any())
        return allEvents.AsReadOnly();
    
    return new CalendarError($"All {errors.Count} sources failed");
}
```

### Validation Chain
```csharp
public Result<MeetingEvent, CalendarError> CreateMeeting(string id, string title)
{
    return ValidateId(id)
        .Bind(_ => ValidateTitle(title))
        .Bind(_ => CreateMeetingInternal(id, title));
}
```

### Try Pattern (Catching Exceptions)
```csharp
public Result<MeetingEvent, Exception> ParseMeeting(string json)
{
    return Result.Try(() => JsonSerializer.Deserialize<MeetingEvent>(json));
}
```

## Error Types

### Domain-Specific Errors
```csharp
public sealed record CalendarError(
    string Message,
    string? CalendarSource = null,
    Exception? InnerException = null) : Error(Message);

public sealed record NotificationError(
    string Message,
    string? StrategyName = null) : Error(Message);

public sealed record ConfigurationError(
    string Message,
    string? ConfigKey = null) : Error(Message);
```

### Creating Errors with Context
```csharp
return new CalendarError(
    "Failed to fetch events",
    CalendarSource: "Google Calendar",
    InnerException: ex);
```

## Best Practices

### DO:
- Return Result types from all operations that can fail
- Use implicit conversions for clean syntax
- Chain operations with Map/Bind for readability
- Include context in error types
- Use OnSuccess/OnError for side effects

### DON'T:
- Throw exceptions for expected error conditions
- Use Result for operations that cannot fail
- Ignore error cases (always handle both paths)
- Create generic error messages without context
- Mix exceptions and Result pattern in the same layer

## Async Operations

### Async Result Methods
```csharp
public async Task<Result<MeetingEvent, CalendarError>> 
    FetchMeetingAsync(string id)
{
    try
    {
        var meeting = await _api.GetMeetingAsync(id);
        return meeting;
    }
    catch (HttpRequestException ex)
    {
        return new CalendarError("Network error", InnerException: ex);
    }
}
```

### Chaining Async Results
```csharp
var result = await FetchMeetingAsync(id);
var processedResult = result
    .Map(meeting => ProcessMeeting(meeting))
    .OnError(error => LogError(error));
```

## Unit Type for Void Results

When an operation doesn't return a value but can fail:

```csharp
public Result<Unit, NotificationError> AcknowledgeMeeting(string id)
{
    var meeting = FindMeeting(id);
    if (meeting == null)
        return new NotificationError("Meeting not found");
    
    meeting.Acknowledge();
    return Unit.Value; // Represents successful void operation
}
```

## Testing Results

### Testing Success
```csharp
[Fact]
public void GetMeeting_WithValidId_ReturnsSuccess()
{
    var result = GetMeeting("valid-id");
    
    Assert.True(result.IsSuccess);
    result.Switch(
        meeting => Assert.Equal("Expected Title", meeting.Title),
        error => Assert.Fail("Should not be an error"));
}
```

### Testing Errors
```csharp
[Fact]
public void GetMeeting_WithInvalidId_ReturnsError()
{
    var result = GetMeeting("");
    
    Assert.True(result.IsError);
    result.Switch(
        meeting => Assert.Fail("Should not succeed"),
        error => Assert.Contains("required", error.Message));
}
```
