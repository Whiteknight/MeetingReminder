using Microsoft.Toolkit.Uwp.Notifications;
using MeetingReminder.Domain.Notifications;
using MeetingReminder.Infrastructure.Notifications;

namespace MeetingReminder.Infrastructure.Windows.Notifications;

/// <summary>
/// Windows implementation of system notifications using Microsoft.Toolkit.Uwp.Notifications.
/// </summary>
public class NotificationProvider : ISystemNotificationProvider
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public Task ShowNotificationAsync(string title, string body, NotificationLevel level)
    {
        if (!IsSupported)
            return Task.CompletedTask;

        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(body);

        builder.Show();

        return Task.CompletedTask;
    }
}
