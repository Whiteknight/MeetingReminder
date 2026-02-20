using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Browser;
using MeetingReminder.Infrastructure.Notifications;
using MeetingReminder.Infrastructure.Windows.Notifications;

var testMeeting = new MeetingEvent(
    id: "test-123",
    title: "Test Meeting",
    startTime: DateTime.Now.AddMinutes(5),
    endTime: DateTime.Now.AddMinutes(65),
    description: "This is a test meeting for notification testing",
    location: "Conference Room A",
    isAllDay: false,
    calendarSource: "manual-test");

var results = new List<TestResult>();

Console.WriteLine("=== Manual Test Suite ===");
Console.WriteLine();
Console.WriteLine("Select test category:");
Console.WriteLine("  (1) Notification Tests");
Console.WriteLine("  (2) Browser Launcher Tests");
Console.WriteLine("  (A) All Tests");
Console.Write("> ");

var categoryKey = Console.ReadKey(true);
Console.WriteLine(categoryKey.KeyChar);
Console.WriteLine();

var runNotificationTests = categoryKey.KeyChar is '1' or 'a' or 'A';
var runBrowserTests = categoryKey.KeyChar is '2' or 'a' or 'A';

if (runNotificationTests)
{
    Console.WriteLine("=== Notification Tests ===");
    Console.WriteLine();

    // Test 1: Beep - Gentle
    results.Add(await RunNotificationTest(
        "Beep Notification - Gentle",
        "You should hear a single short beep at low frequency (500Hz)",
        new BeepNotificationStrategy(),
        NotificationLevel.Gentle,
        testMeeting));

    // Test 2: Beep - Critical
    results.Add(await RunNotificationTest(
        "Beep Notification - Critical",
        "You should hear 4 rapid beeps at high frequency (1200Hz)",
        new BeepNotificationStrategy(),
        NotificationLevel.Critical,
        testMeeting));

    // Test 3: Terminal Flash - Gentle
    results.Add(await RunNotificationTest(
        "Terminal Flash - Gentle",
        "The console window/taskbar should flash briefly (2 times)",
        new TerminalFlashStrategy(),
        NotificationLevel.Gentle,
        testMeeting));

    // Test 4: Terminal Flash - Critical
    results.Add(await RunNotificationTest(
        "Terminal Flash - Critical",
        "The console window/taskbar should flash many times (may appear as 2 flashes on Windows Terminal)",
        new TerminalFlashStrategy(),
        NotificationLevel.Critical,
        testMeeting));

    // Test 5: System Notification - Gentle
    results.Add(await RunNotificationTest(
        "System Notification - Gentle",
        "A Windows toast notification should appear with '📅 Upcoming: Test Meeting'",
        new SystemNotificationStrategy(new NotificationProvider()),
        NotificationLevel.Gentle,
        testMeeting));

    // Test 6: System Notification - Critical
    results.Add(await RunNotificationTest(
        "System Notification - Critical",
        "A Windows toast notification should appear with '🚨 NOW: Test Meeting'",
        new SystemNotificationStrategy(new NotificationProvider()),
        NotificationLevel.Critical,
        testMeeting));
}

if (runBrowserTests)
{
    Console.WriteLine("=== Browser Launcher Tests ===");
    Console.WriteLine();

    var browserLauncher = new SystemBrowserLauncher();

    // Browser Test 1: Simple HTTPS URL
    results.Add(RunBrowserTest(
        "Browser - Simple HTTPS URL",
        "Your default browser should open to https://example.com",
        browserLauncher,
        "https://example.com"));

    // Browser Test 2: Google Meet URL
    results.Add(RunBrowserTest(
        "Browser - Google Meet URL",
        "Your default browser should open to a Google Meet page (may show error page since it's a fake meeting)",
        browserLauncher,
        "https://meet.google.com/abc-defg-hij"));

    // Browser Test 3: URL with query parameters
    results.Add(RunBrowserTest(
        "Browser - URL with Query Parameters",
        "Your default browser should open to https://example.com/page?param=value",
        browserLauncher,
        "https://example.com/page?param=value"));
}

// Print summary
PrintSummary(results);

return results.All(r => r.Passed) ? 0 : 1;

static async Task<TestResult> RunNotificationTest(
    string testName,
    string expectation,
    INotificationStrategy strategy,
    NotificationLevel level,
    MeetingEvent meeting)
{
    while (true)
    {
        Console.WriteLine($"--- {testName} ---");
        Console.WriteLine($"Expected: {expectation}");
        Console.WriteLine();

        if (!strategy.IsSupported)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("SKIPPED: Strategy not supported on this platform");
            Console.ResetColor();
            Console.WriteLine();
            return new TestResult(testName, TestOutcome.Skipped);
        }

        Console.WriteLine("Press any key to execute the notification...");
        Console.ReadKey(true);

        // Execute both cycle and level-change methods to test the strategy
        var cycleResult = await strategy.ExecuteOnCycleAsync(level, meeting);
        var levelChangeResult = await strategy.ExecuteOnLevelChangeAsync(NotificationLevel.None, level, meeting);

        if (cycleResult.IsError)
        {
            var error = cycleResult.Match(_ => "", e => e.Message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Cycle ERROR: {error}");
            Console.ResetColor();
        }

        if (levelChangeResult.IsError)
        {
            var error = levelChangeResult.Match(_ => "", e => e.Message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Level Change ERROR: {error}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("Did you see/hear the notification?");
        Console.WriteLine("  (Y) Yes - it worked");
        Console.WriteLine("  (N) No - it didn't work");
        Console.WriteLine("  (R) Retry - run it again");
        Console.Write("> ");

        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);
        Console.WriteLine();

        switch (char.ToLower(key.KeyChar))
        {
            case 'y':
                return new TestResult(testName, TestOutcome.Passed);
            case 'n':
                return new TestResult(testName, TestOutcome.Failed);
            case 'r':
                continue;
            default:
                Console.WriteLine("Invalid input, treating as retry...");
                continue;
        }
    }
}

static void PrintSummary(List<TestResult> results)
{
    Console.WriteLine();
    Console.WriteLine("=== Test Summary ===");
    Console.WriteLine();

    foreach (var result in results)
    {
        Console.ForegroundColor = result.Outcome switch
        {
            TestOutcome.Passed => ConsoleColor.Green,
            TestOutcome.Failed => ConsoleColor.Red,
            TestOutcome.Skipped => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };

        var symbol = result.Outcome switch
        {
            TestOutcome.Passed => "✓",
            TestOutcome.Failed => "✗",
            TestOutcome.Skipped => "○",
            _ => "?"
        };

        Console.WriteLine($"  {symbol} {result.TestName}");
        Console.ResetColor();
    }

    Console.WriteLine();

    var passed = results.Count(r => r.Outcome == TestOutcome.Passed);
    var failed = results.Count(r => r.Outcome == TestOutcome.Failed);
    var skipped = results.Count(r => r.Outcome == TestOutcome.Skipped);

    Console.WriteLine($"Passed: {passed}, Failed: {failed}, Skipped: {skipped}");
}

static TestResult RunBrowserTest(
    string testName,
    string expectation,
    SystemBrowserLauncher browserLauncher,
    string url)
{
    while (true)
    {
        Console.WriteLine($"--- {testName} ---");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine($"Expected: {expectation}");
        Console.WriteLine();

        Console.WriteLine("Press any key to open the URL in your browser...");
        Console.ReadKey(true);

        var result = browserLauncher.OpenUrl(url);

        if (result.IsError)
        {
            var error = result.Match(_ => "", e => e.Message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {error}");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Browser launch command executed successfully.");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine("Did the browser open to the correct URL?");
        Console.WriteLine("  (Y) Yes - it worked");
        Console.WriteLine("  (N) No - it didn't work");
        Console.WriteLine("  (R) Retry - run it again");
        Console.Write("> ");

        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);
        Console.WriteLine();

        switch (char.ToLower(key.KeyChar))
        {
            case 'y':
                return new TestResult(testName, TestOutcome.Passed);
            case 'n':
                return new TestResult(testName, TestOutcome.Failed);
            case 'r':
                continue;
            default:
                Console.WriteLine("Invalid input, treating as retry...");
                continue;
        }
    }
}

record TestResult(string TestName, TestOutcome Outcome)
{
    public bool Passed => Outcome == TestOutcome.Passed;
}

enum TestOutcome { Passed, Failed, Skipped }
