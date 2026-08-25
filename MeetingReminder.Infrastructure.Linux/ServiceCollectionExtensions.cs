using MeetingReminder.Domain.Browsers;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Browser;
using MeetingReminder.Infrastructure.Notifications;
using MeetingReminder.Infrastructure.Linux.Browser;
using MeetingReminder.Infrastructure.Linux.Configuration;
using MeetingReminder.Infrastructure.Linux.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingReminder.Infrastructure.Linux;

/// <summary>
/// DI registrations for all Linux-specific infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Linux-specific services: config path resolver, browser launcher,
    /// and notification strategies.
    /// </summary>
    public static IServiceCollection AddLinuxPlatformServices(
        this IServiceCollection services,
        IConfigPathResolver pathResolver)
    {
        services.AddSingleton(pathResolver);
        services.AddSingleton<IBrowserLauncher, LinuxBrowserLauncher>();
        services.AddSingleton<ISystemNotificationProvider, NotificationProvider>();
        services.AddSingleton<INotificationStrategy, SystemNotificationStrategy>();
        services.AddSingleton<INotificationStrategy, TerminalFlashStrategy>();
        return services;
    }
}
