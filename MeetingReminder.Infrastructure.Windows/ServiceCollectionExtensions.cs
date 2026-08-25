using MeetingReminder.Domain.Browsers;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Browser;
using MeetingReminder.Infrastructure.Notifications;
using MeetingReminder.Infrastructure.Windows.Browser;
using MeetingReminder.Infrastructure.Windows.Configuration;
using MeetingReminder.Infrastructure.Windows.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingReminder.Infrastructure.Windows;

/// <summary>
/// DI registrations for all Windows-specific infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Windows-specific services: config path resolver, browser launcher,
    /// and notification strategies.
    /// </summary>
    public static IServiceCollection AddWindowsPlatformServices(
        this IServiceCollection services,
        IConfigPathResolver pathResolver)
    {
        services.AddSingleton(pathResolver);
        services.AddSingleton<IBrowserLauncher, WindowsBrowserLauncher>();
        services.AddSingleton<ISystemNotificationProvider, NotificationProvider>();
        services.AddSingleton<INotificationStrategy, SystemNotificationStrategy>();
        services.AddSingleton<INotificationStrategy, BeepNotificationStrategy>();
        services.AddSingleton<INotificationStrategy, TerminalFlashStrategy>();
        return services;
    }
}
