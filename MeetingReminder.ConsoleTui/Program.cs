using System.Text;
using MeetingReminder.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MeetingReminder.ConsoleTui;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");

        // Validate configuration before starting the host
        var configValidationResult = ValidateConfiguration(configPath);
        if (!configValidationResult)
        {
            AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
            Console.ReadKey(true);
            return;
        }

        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C for graceful shutdown
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("[yellow]Shutdown requested...[/]");
            cts.Cancel();
        };

        try
        {
            var host = CreateHostBuilder(args, configPath).Build();
            await host.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            AnsiConsole.MarkupLine("[grey]Application shutdown complete.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fatal error:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine($"[red]Type:[/] {ex.GetType().FullName}");
            if (ex.InnerException != null)
                AnsiConsole.MarkupLine($"[red]Inner:[/] {Markup.Escape(ex.InnerException.Message)}");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(ex.StackTrace ?? "")}[/]");
            AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
            Console.ReadKey(true);
        }
    }

    /// <summary>
    /// Validates the configuration file before starting the application.
    /// Logs any validation errors to the console.
    /// </summary>
    /// <param name="configPath">Path to the configuration file</param>
    /// <returns>True if configuration is valid or defaults are used, false if fatal errors</returns>
    private static bool ValidateConfiguration(string configPath)
    {
        var configManager = new JsonConfigurationManager(configPath);
        var result = configManager.LoadConfiguration();

        return result.Match(
            _ => true,
            error =>
            {
                AnsiConsole.MarkupLine($"[red]Configuration error:[/] {Markup.Escape(error.Message)}");

                if (error.ConfigKey != null)
                {
                    AnsiConsole.MarkupLine($"[grey]Config path: {Markup.Escape(error.ConfigKey)}[/]");
                }

                // For validation errors, show details but allow startup with defaults
                if (error.Message.StartsWith("Configuration validation failed"))
                {
                    AnsiConsole.MarkupLine("[yellow]Using default configuration instead.[/]");
                    return true;
                }

                return false;
            });
    }

    private static IHostBuilder CreateHostBuilder(string[] args, string configPath)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                // Suppress console logging to avoid interfering with TUI
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((context, services) =>
            {
                // Core infrastructure (time provider, HTTP client)
                services.AddCoreInfrastructure();

                // Configuration management
                services.AddConfiguration(configPath);

                // Repositories and external services
                services.AddMeetingRepository();
                services.AddBrowserLauncher();
                services.AddCalendarSources();

                // Use cases
                services.AddCalendarUseCases();
                services.AddNotificationUseCases();
                services.AddAcknowledgementUseCases();

                // Background services (run on separate threads via IHostedService)
                // CalendarPollingService: Polls calendars at configured interval
                // NotificationProcessingService: Processes notifications every 10 seconds
                services.AddCalendarPolling();
                services.AddNotificationProcessing();

                // TUI services (BackgroundService pattern - runs on thread pool)
                // MeetingReminderTuiService: Renders the three-panel TUI
                // KeyboardInputService: Handles keyboard input
                services.AddEnhancedTui();
                services.AddKeyboardInput();
            });
    }
}
