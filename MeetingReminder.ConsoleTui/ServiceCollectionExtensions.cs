using MeetingReminder.Application.UseCases;
using MeetingReminder.ConsoleTui.Services;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Input;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Browser;
using MeetingReminder.Infrastructure.Calendars;
using MeetingReminder.Infrastructure.Configuration;
using MeetingReminder.Infrastructure.ICal;
using MeetingReminder.Infrastructure.Meetings;
using MeetingReminder.Infrastructure.Notifications;
using MeetingReminder.Infrastructure.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingReminder.ConsoleTui;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core infrastructure services including time provider and HTTP client.
    /// </summary>
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddHttpClient();
        return services;
    }

    /// <summary>
    /// Adds configuration services and loads configuration from the specified path.
    /// </summary>
    public static IServiceCollection AddConfiguration(
        this IServiceCollection services,
        string configPath)
    {
        services.AddSingleton<IConfigurationManager>(
            _ => new JsonConfigurationManager(configPath));

        services.AddSingleton<IAppConfiguration>(sp =>
        {
            var configManager = sp.GetRequiredService<IConfigurationManager>();
            var result = configManager.LoadConfiguration();
            return result.Match(
                config => config,
                _ => AppConfiguration.Default);
        });

        return services;
    }

    /// <summary>
    /// Adds calendar source implementations based on configuration.
    /// </summary>
    public static IServiceCollection AddCalendarSources(this IServiceCollection services)
    {
        services.AddSingleton<IEnumerable<ICalendarSource>>(sp =>
        {
            var config = sp.GetRequiredService<IAppConfiguration>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var sources = new List<ICalendarSource>();

            foreach (var calendarConfig in config.Calendars)
            {
                if (string.IsNullOrEmpty(calendarConfig.SourceUrl))
                    continue;

                var httpClient = httpClientFactory.CreateClient(calendarConfig.Name);
                sources.Add(new IcsCalendarSource(httpClient, calendarConfig.SourceUrl, calendarConfig.Name));
            }

            return sources;
        });

        return services;
    }

    /// <summary>
    /// Adds the FetchCalendarEvents use case.
    /// </summary>
    public static IServiceCollection AddCalendarUseCases(this IServiceCollection services)
    {
        services.AddSingleton<FetchCalendarEvents>();

        return services;
    }

    /// <summary>
    /// Adds the calendar polling service as a hosted service.
    /// </summary>
    public static IServiceCollection AddCalendarPolling(this IServiceCollection services)
    {
        services.AddSingleton<ICalendarPollingService, CalendarPollingService>();

        services.AddHostedService<CalendarPollingHostedService>();

        return services;
    }

    /// <summary>
    /// Adds the enhanced three-panel TUI service.
    /// The event loop reads key presses via IKeyboardInputHandler (pure mapper),
    /// then handles the resulting TuiCommand internally.
    /// </summary>
    public static IServiceCollection AddEnhancedTui(this IServiceCollection services)
    {
        services.AddSingleton<MeetingReminderTuiService>();
        services.AddHostedService(sp => sp.GetRequiredService<MeetingReminderTuiService>());
        return services;
    }

    /// <summary>
    /// Adds the meeting repository for storing meeting state.
    /// </summary>
    public static IServiceCollection AddMeetingRepository(this IServiceCollection services)
    {
        services.AddSingleton<IMeetingRepository, InMemoryMeetingRepository>();
        services.AddSingleton<IChangeNotifier, AsyncAutoResetEvent>();
        return services;
    }

    /// <summary>
    /// Adds the browser launcher for opening meeting links.
    /// </summary>
    public static IServiceCollection AddBrowserLauncher(this IServiceCollection services)
    {
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        return services;
    }

    /// <summary>
    /// Adds the AcknowledgeMeeting use case.
    /// </summary>
    public static IServiceCollection AddAcknowledgementUseCases(this IServiceCollection services)
    {
        services.AddSingleton<AcknowledgeMeeting>();

        return services;
    }

    /// <summary>
    /// Adds the keyboard input service as a pure key-to-command mapper.
    /// No dependencies on TUI state or application lifetime.
    /// </summary>
    public static IServiceCollection AddKeyboardInput(this IServiceCollection services)
    {
        services.AddSingleton<KeyboardInputService>();
        services.AddSingleton<IKeyboardInputHandler>(sp => sp.GetRequiredService<KeyboardInputService>());
        return services;
    }

    /// <summary>
    /// Adds notification-related use cases.
    /// </summary>
    public static IServiceCollection AddNotificationUseCases(this IServiceCollection services)
    {
        services.AddSingleton<CalculateNotificationLevel>();
        return services;
    }

    /// <summary>
    /// Adds the notification processing service.
    /// </summary>
    public static IServiceCollection AddNotificationProcessing(this IServiceCollection services)
    {
        // Register platform-specific notification strategies
        services.AddNotificationStrategies();

        services.AddSingleton<NotificationProcessingService>();

        services.AddHostedService<NotificationProcessingHostedService>();

        return services;
    }

    /// <summary>
    /// Adds platform-specific notification strategies.
    /// </summary>
    public static IServiceCollection AddNotificationStrategies(this IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows-specific notification providers
            services.AddSingleton<ISystemNotificationProvider, MeetingReminder.Infrastructure.Windows.Notifications.NotificationProvider>();
            services.AddSingleton<INotificationStrategy, SystemNotificationStrategy>();
            services.AddSingleton<INotificationStrategy, MeetingReminder.Infrastructure.Windows.Notifications.BeepNotificationStrategy>();
            return services;
        }

        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IEnumerable<INotificationStrategy>>(_ => []);
        }

        return services;
    }
}
