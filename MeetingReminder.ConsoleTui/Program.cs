using System.Text;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace MeetingReminder.ConsoleTui;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "nag";

        var pathResolver = new ConfigPathResolver();

        // Validate configuration before starting the host
        var configValidationResult = ValidateConfiguration(pathResolver);
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
            var host = CreateHostBuilder(args, pathResolver).Build();
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
    /// Handles first-run setup (config creation + exit signal).
    /// </summary>
    private static bool ValidateConfiguration(ConfigPathResolver pathResolver)
    {
        var configManager = new YamlConfigurationManager(pathResolver);
        var result = configManager.LoadConfiguration();

        return result.Match(
            _ => true,
            error =>
            {
                if (error is FirstRunConfigurationError)
                {
                    AnsiConsole.MarkupLine($"[green]First run detected.[/]");
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(error.Message)}[/]");
                    AnsiConsole.MarkupLine($"[grey]A template file with examples has also been created at:[/]");
                    AnsiConsole.MarkupLine($"[grey]{Markup.Escape(pathResolver.GetTemplateFilePath())}[/]");
                    return false;
                }

                AnsiConsole.MarkupLine($"[red]Configuration error:[/] {Markup.Escape(error.Message)}");

                if (error.ConfigKey != null)
                    AnsiConsole.MarkupLine($"[grey]Config path: {Markup.Escape(error.ConfigKey)}[/]");

                if (error.Message.StartsWith("Configuration validation failed"))
                {
                    AnsiConsole.MarkupLine("[yellow]Using default configuration instead.[/]");
                    return true;
                }

                return false;
            });
    }

    private static IHostBuilder CreateHostBuilder(string[] args, ConfigPathResolver pathResolver)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureServices((_, services) =>
            {
                services.AddCoreInfrastructure();
                services.AddConfiguration(pathResolver);
                services.AddMeetingRepository();
                services.AddBrowserLauncher();
                services.AddCalendarSources();
                services.AddCalendarUseCases();
                services.AddNotificationUseCases();
                services.AddAcknowledgementUseCases();
                services.AddCalendarPolling();
                services.AddNotificationProcessing();
                services.AddEnhancedTui();
                services.AddKeyboardInput();
            });
    }
}
