# Implementation Plan: Meeting Reminder TUI

## Overview

This implementation plan breaks down the Meeting Reminder TUI application into discrete coding tasks following Clean Architecture principles with vertical slices. The application uses C# with Spectre.Console for the TUI, the Result pattern for error handling, and a pub/sub model for thread communication.

## Tasks

- [x] 1. Set up project structure and core infrastructure
  - Create solution with multiple projects following Clean Architecture
  - Set up Domain, Application, Infrastructure, and vendor-specific projects
  - Configure project dependencies (Domain has no dependencies, Application depends only on Domain)
  - Add NuGet packages: Spectre.Console, Ical.Net, Google.Apis.Calendar.v3, System.Text.Json
  - _Requirements: 11.1, 11.2, 11.4_

- [x] 2. Implement Result pattern and core domain models
  - [x] 2.1 Implement Result<T> and Result<T, TError> types
    - Create generic Result types with Success/Failure factory methods
    - Implement IsSuccess, IsFailure properties
    - Add Value and Error properties with appropriate access
    - _Requirements: 11.1_
  
  - [x] 2.2 Implement Error base type and domain-specific errors
    - Create Error base record with Code, Message, and Context
    - Implement CalendarError, NotificationError, ConfigurationError
    - _Requirements: 12.1, 12.2, 12.3_
  
  - [x] 2.3 Implement core domain entities
    - Create MeetingEvent entity with all properties
    - Implement MeetingLink value object
    - Create NotificationLevel enum
    - Implement MeetingState entity with domain logic methods
    - _Requirements: 1.2, 2.2, 8.5_
  
  - [ ]* 2.4 Write property test for MeetingEvent domain logic
    - **Property 1: Chronological Meeting Order**
    - **Validates: Requirements 1.2**
  
  - [x] 2.5 Implement message types for channel communication
    - Create CalendarEventsUpdated record
    - Create NotificationStateChanged record
    - Create MeetingAcknowledged record
    - _Requirements: 7.4, 8.4_

- [x] 3. Implement configuration management
  - [x] 3.1 Create configuration models
    - Implement AppConfiguration record
    - Implement NotificationThresholds record
    - Implement CalendarConfiguration record
    - Implement CalendarNotificationRules with time window logic
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.8_
  
  - [x] 3.2 Implement configuration loader
    - Create IConfigurationManager interface
    - Implement JSON-based configuration loading
    - Return default configuration if file missing
    - Handle JSON parsing errors with Result pattern
    - _Requirements: 10.1, 10.5_
  
  - [ ]* 3.3 Write property test for configuration validation
    - **Property 28: Configuration Application**
    - **Validates: Requirements 10.2, 10.3, 10.4, 10.5**
  
  - [x] 3.4 Write unit tests for configuration loading

    - Test default configuration when file missing
    - Test JSON parsing errors
    - Test invalid configuration values

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement meeting link extraction use case
  - [x] 5.1 Create ExtractMeetingLinkQuery and handler
    - Implement regex patterns for Google Meet, Zoom, Teams
    - Prioritize video conferencing links over generic URLs
    - Return Result<MeetingLink?>
    - _Requirements: 4.1, 4.2, 4.3_
  
  - [ ]* 5.2 Write property test for link extraction
    - **Property 12: Meeting Link Extraction**
    - **Validates: Requirements 4.1**
  
  - [ ]* 5.3 Write property test for link type recognition
    - **Property 13: Meeting Link Type Recognition**
    - **Validates: Requirements 4.2**
  
  - [ ]* 5.4 Write property test for link prioritization
    - **Property 14: Video Conferencing Link Prioritization**
    - **Validates: Requirements 4.3**
  
  - [x] 5.5 Write unit tests for link extraction edge cases

    - Test events with no links
    - Test events with multiple links
    - Test malformed URLs

- [x] 6. Implement notification level calculation use case
  - [x] 6.1 Create CalculateNotificationLevelQuery and handler
    - Implement threshold-based level calculation
    - Handle all-day event suppression
    - Apply notification time window rules
    - Return Result<NotificationLevel>
    - _Requirements: 8.1, 8.2, 8.3, 8.5, 8.6, 10.8_
  
  - [ ]* 6.2 Write property test for notification escalation
    - **Property 23: Notification Escalation**
    - **Validates: Requirements 8.2, 8.5**
  
  - [ ]* 6.3 Write property test for all-day event suppression
    - **Property 26: All-Day Event Notification Suppression**
    - **Validates: Requirements 8.6, 8.7**
  
  - [ ]* 6.4 Write property test for time window enforcement
    - **Property 30: Notification Time Window Enforcement**
    - **Validates: Requirements 10.8, 10.9**
  
  - [x] 6.5 Write unit tests for notification thresholds

    - Test gentle threshold
    - Test moderate threshold
    - Test urgent threshold
    - Test critical (at start time)

- [x] 7. Implement calendar source abstraction and iCal integration
  - [x] 7.1 Create ICalendarSource interface
    - Define FetchEventsAsync returning Result<IReadOnlyList<MeetingEvent>, CalendarError>
    - Define SourceName property
    - _Requirements: 5.1, 5.2, 6.1_
  
  - [x] 7.2 Implement IcsCalendarSource
    - Fetch iCal/ICS data via HTTP
    - Parse iCal using Ical.Net library
    - Map iCal events to MeetingEvent domain model
    - Handle network errors with Result pattern
    - Handle malformed data with Result pattern
    - _Requirements: 6.1, 6.2, 6.3, 6.5_
  
  - [ ]* 7.3 Write property test for time window filtering
    - **Property 16: Time Window Event Filtering**
    - **Validates: Requirements 5.5**
  
  - [ ]* 7.4 Write property test for iCal round-trip
    - **Property 17: iCal Data Round-Trip**
    - **Validates: Requirements 6.2**
  
  - [ ]* 7.5 Write property test for malformed iCal handling
    - **Property 19: Malformed iCal Handling**
    - **Validates: Requirements 6.5**
  
  - [x] 7.6 Write unit tests for ICS calendar integration

    - Test successful iCal fetch and parse
    - Test unreachable URL handling
    - Test malformed iCal data

- [x] 8. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement fetch calendar events use case
  - [x] 9.1 Create FetchCalendarEventsQuery and handler
    - Accept multiple ICalendarSource instances
    - Fetch from all sources concurrently
    - Aggregate results, succeeding if at least one source works
    - Return Result<IReadOnlyList<MeetingEvent>, CalendarError>
    - _Requirements: 6.4, 12.4_
  
  - [ ]* 9.2 Write property test for multiple calendar support
    - **Property 18: Multiple Calendar Source Support**
    - **Validates: Requirements 6.4**
  
  - [ ]* 9.3 Write property test for calendar source failure resilience
    - **Property 32: Calendar Source Failure Resilience**
    - **Validates: Requirements 12.4**

- [x] 10. Implement minimal TUI to display calendar events (Early Preview)
  - [ ] 10.1 Create IUserInterface abstraction
    - Define RunAsync method
    - Define method to update meeting list
    - _Requirements: 11.2, 11.3_
  
  - [x] 10.2 Implement basic SpectreConsoleTUI
    - Set up simple layout using Spectre.Console
    - Display table of upcoming meetings (title, start time, end time)
    - Show "No upcoming meetings" when list is empty
    - Support Q key to quit
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 13.1_
  
  - [x] 10.3 Wire up Program.cs for early preview
    - Load configuration
    - Create calendar sources from configuration
    - Fetch events once on startup
    - Display events in TUI
    - _Requirements: 10.1, 10.5_
  
  - [x] 10.4 Checkpoint - Run application and verify calendar display
    - Ensure application starts and displays calendar events
    - Verify Q key exits cleanly

- [x] 11. Implement calendar polling service
  - [x] 11.1 Create CalendarPollingService
    - Use Timer for periodic polling at configured interval
    - Call FetchCalendarEventsHandler
    - Detect added and removed meetings
    - Write CalendarEventsUpdated messages to calendar channel
    - Use SemaphoreSlim to prevent overlapping polls
    - _Requirements: 7.1, 7.3, 7.4, 7.5_
  
  - [ ]* 11.2 Write property test for polling interval adherence
    - **Property 20: Polling Interval Adherence**
    - **Validates: Requirements 7.1, 7.3**
  
  - [ ]* 11.3 Write property test for removed meeting notification termination
    - **Property 21: Removed Meeting Notification Termination**
    - **Validates: Requirements 7.4**
  
  - [x] 11.4 Write unit tests for polling service

    - Test default 5-minute interval
    - Test custom interval configuration
    - Test meeting change detection

- [x] 12. Integrate CalendarPollingService with TUI
  - [x] 12.1 Set up Generic Host and DI container
    - Add Microsoft.Extensions.Hosting NuGet package to ConsoleTui project
    - Create HostBuilder with ConfigureServices
    - Register ITimeProvider, HttpClient, and configuration services
    - _Requirements: 11.4_
  
  - [x] 12.2 Register calendar polling infrastructure
    - Register ICalendarSource implementations from configuration
    - Register FetchCalendarEvents use case
    - Register Channel<CalendarEventsUpdated> for pub/sub
    - Register CalendarPollingService as IHostedService
    - _Requirements: 7.1, 11.2_
  
  - [x] 12.3 Create CalendarDisplayService for TUI updates
    - Create hosted service that reads from CalendarEventsUpdated channel
    - Maintain current list of MeetingEvents
    - Render Spectre.Console table when events update
    - Clear and redraw table on each update
    - _Requirements: 1.1, 1.4, 13.5_
  
  - [x] 12.4 Update Program.cs to use Generic Host
    - Replace manual setup with Host.CreateDefaultBuilder
    - Start host and run until cancellation
    - Handle graceful shutdown with Ctrl+C
    - _Requirements: 11.2, 12.5_
  
  - [x] 12.5 Checkpoint - Run application and verify live calendar updates
    - Ensure application starts and displays calendar events
    - Verify events refresh automatically at polling interval
    - Verify Ctrl+C exits cleanly

- [x] 13. Implement notification strategies
  - [x] 13.1 Create INotificationStrategy interface
    - Define ExecuteAsync returning Result<Unit, NotificationError>
    - Define StrategyName and IsSupported properties
    - _Requirements: 9.1, 9.2_
  
  - [x] 13.2 Implement BeepNotificationStrategy
    - Use Console.Beep with varying frequency/duration by level
    - Check platform support
    - _Requirements: 9.5_
  
  - [x] 13.3 Implement TerminalFlashStrategy
    - Windows: Use Win32 API FlashWindowEx
    - Linux: Use X11 or notify-send
    - Check platform support
    - _Requirements: 9.4_
  
  - [x] 13.4 Implement SoundFileStrategy
    - Play audio files with escalating intensity
    - Use NAudio on Windows or similar on Linux
    - Check platform support
    - _Requirements: 9.6_
  
  - [x] 13.5 Implement SystemNotificationStrategy
    - Windows: Use Windows.UI.Notifications
    - Linux: Use libnotify or notify-send
    - Check platform support
    - _Requirements: 9.7, 9.8_
  
  - [x] 13.6 Write unit tests for notification strategies

    - Test platform compatibility checks
    - Test strategy execution
    - Test error handling

- [x] 14. Implement notification processing service
  - [x] 14.1 Create NotificationProcessingService
    - Read from calendar channel in background task
    - Maintain dictionary of MeetingState
    - Use Timer to process notifications every 10 seconds
    - Calculate notification levels for all unacknowledged meetings
    - Execute enabled notification strategies
    - Write NotificationStateChanged messages to notification channel
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 9.2, 9.3_
  
  - [ ]* 14.2 Write property test for enabled strategies execution
    - **Property 27: Enabled Strategies Execution**
    - **Validates: Requirements 9.2, 9.3**
  
  - [ ]* 14.3 Write property test for notification strategy failure isolation
    - **Property 31: Notification Strategy Failure Isolation**
    - **Validates: Requirements 12.3**
  
  - [ ]* 14.4 Write property test for persistent critical notifications
    - **Property 25: Persistent Critical Notifications**
    - **Validates: Requirements 8.4**

- [x] 15. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 16. Implement acknowledge meeting use case
  - [x] 16.1 Create AcknowledgeMeetingCommand and handler
    - Accept meeting ID and openLink flag
    - Get meeting state from repository
    - Open browser if requested using IBrowserLauncher
    - Mark meeting as acknowledged
    - Write MeetingAcknowledged message to acknowledgement channel
    - Return Result<Unit, NotificationError>
    - _Requirements: 3.2, 3.7_
  
  - [x] 16.2 Implement IBrowserLauncher and SystemBrowserLauncher
    - Windows: Use Process.Start with UseShellExecute
    - Linux: Use xdg-open
    - macOS: Use open command
    - Return Result<Unit, Error>
    - _Requirements: 4.5_
  
  - [ ]* 16.3 Write property test for acknowledgement terminating notifications
    - **Property 6: Acknowledgement Terminates Notifications**
    - **Validates: Requirements 3.2**
  
  - [ ]* 16.4 Write property test for open link action completeness
    - **Property 10: Open Link Action Completeness**
    - **Validates: Requirements 3.7**
  
  - [x] 16.5 Write unit tests for browser launching

    - Test URL opening on different platforms
    - Test error handling for invalid URLs

- [x] 17. Implement meeting repository ✓
  - [x] 17.1 Create IMeetingRepository interface ✓
    - Define GetByIdAsync, GetAllAsync, UpdateAsync
    - All methods return Result types
    - _Requirements: 3.5_
  
  - [x] 17.2 Implement InMemoryMeetingRepository ✓
    - Use ConcurrentDictionary for thread-safe storage
    - Implement all interface methods
    - _Requirements: 3.5_

- [x] 18. Enhance TUI with full functionality
  - [x] 18.1 Enhance SpectreConsoleTUI with three-panel layout
    - Top panel: Table of upcoming meetings
    - Middle panel: Detailed meeting view
    - Bottom panel: Acknowledgement instructions
    - Subscribe to CalendarEventsUpdated and NotificationStateChanged events
    - Update display in real-time
    - _Requirements: 1.1, 2.1, 3.1, 13.2, 13.5_
  
  - [x] 18.2 Implement meeting detail view rendering
    - Display title, start time, end time, description
    - Show meeting link indicator if present
    - Default to next upcoming meeting when none selected
    - Update when selection changes
    - _Requirements: 2.2, 2.4, 4.4_
  
  - [ ]* 18.3 Write property test for meeting display information
    - **Property 2: Meeting Display Contains Required Information**
    - **Validates: Requirements 1.3**
  
  - [ ]* 18.4 Write property test for meeting details
    - **Property 4: Meeting Details Contain All Required Fields**
    - **Validates: Requirements 2.2**
  
  - [ ]* 18.5 Write property test for link presence indication
    - **Property 15: Link Presence Indication**
    - **Validates: Requirements 4.4**
  
  - [x] 18.6 Implement acknowledgement area rendering
    - Show keyboard shortcuts (Enter to acknowledge, O to open link)
    - Display multiple imminent meetings ordered by urgency
    - Show acknowledgement confirmation
    - _Requirements: 3.3, 3.6, 3.9_
  
  - [ ]* 18.7 Write property test for urgency-based ordering
    - **Property 11: Urgency-Based Meeting Ordering**
    - **Validates: Requirements 3.9, 3.10**
  
  - [ ]* 18.8 Write property test for meeting link display options
    - **Property 9: Meeting Link Display Options**
    - **Validates: Requirements 3.6**

- [x] 19. Implement keyboard input handling
  - [x] 19.1 Implement keyboard event loop
    - Arrow keys: Navigate meeting list
    - Enter: Acknowledge selected/next meeting
    - O: Open link and acknowledge
    - Q: Quit application
    - Handle invalid keys gracefully
    - _Requirements: 3.8, 13.4_
  
  - [x] 19.2 Wire keyboard actions to use cases
    - Call AcknowledgeMeetingHandler on Enter/O
    - Update selected meeting on arrow keys
    - _Requirements: 2.3, 3.5_
  
  - [ ]* 19.3 Write property test for manual selection updating detail view
    - **Property 5: Manual Selection Updates Detail View**
    - **Validates: Requirements 2.3**
  
  - [x] 19.4 Write unit tests for keyboard shortcuts

    - Test Enter key acknowledgement
    - Test O key open and acknowledge
    - Test arrow key navigation
    - Test Q key quit

- [x] 20. Wire up full application with threading
  - [x] 20.1 Update Program.cs with full dependency injection
    - Set up dependency injection container
    - Register all services (event bus, repositories, handlers, strategies)
    - Load configuration
    - Validate configuration
    - _Requirements: 10.1, 10.5_
  
  - [x] 20.2 Wire up threading model
    - Start calendar polling service on background thread
    - Start notification processing service on background thread
    - Run TUI on main thread
    - Handle graceful shutdown
    - _Requirements: 11.2_
  
  - [x] 20.3 Implement application lifecycle management
    - Handle CancellationToken for shutdown
    - Dispose services properly
    - Save state if needed
    - _Requirements: 12.5_

- [ ] 21. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 22. Integration testing and polish
  - [ ]* 22.1 Write integration tests
    - Test end-to-end calendar polling to notification flow
    - Test acknowledgement flow
    - Test configuration loading and application
  
  - [x] 22.2 Add logging throughout application
    - Log calendar fetch operations
    - Log notification executions
    - Log errors with context
    - _Requirements: 12.1, 12.2, 12.3_
  
  - [ ] 22.3 Create default configuration file
    - Generate sensible defaults
    - Include comments explaining each setting
    - Save to ~/.meeting-reminder/config.json
    - _Requirements: 10.5_
  
  - [ ] 22.4 Add README with setup instructions
    - Document Google Calendar OAuth setup
    - Document iCal URL configuration
    - Document notification strategy configuration
    - Document keyboard shortcuts

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck
- Unit tests validate specific examples and edge cases
- The Result pattern is used throughout instead of exceptions
- Threading model uses `System.Threading.Channels` for thread-safe, async-friendly communication between threads
- Clean Architecture ensures domain has no external dependencies
