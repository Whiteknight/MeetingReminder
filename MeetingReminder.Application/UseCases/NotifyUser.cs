using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Application.UseCases;

public sealed class NotifyUser
{
    private readonly IEnumerable<INotificationStrategy> _enabledStrategies;

    public NotifyUser(
        IAppConfiguration config,
        IEnumerable<INotificationStrategy> strategies)
    {
        // Filter to only enabled and supported strategies (Requirements 9.2, 9.3)
        _enabledStrategies = strategies
            .Where(s => config.NotificationStrategyIsEnabled(s.StrategyName) && s.IsSupported)
            .ToList();
    }

    public async Task<Result<Unit, Error>> Notify(IReadOnlyList<MeetingState> meetings)
    {
        var errors = new List<Error>();
        foreach (var strategy in _enabledStrategies)
            await ExecuteStrategy(meetings, errors, strategy);
        return errors.Count == 0
            ? Unit.Value
            : Error.Flatten(errors);
    }

    private static async Task ExecuteStrategy(IReadOnlyList<MeetingState> meetings, List<Error> errors, INotificationStrategy strategy)
    {
        try
        {
            await TryExecuteStrategy(meetings, strategy, errors);
        }
        catch (Exception ex)
        {
            // Catch any unexpected exceptions to ensure other strategies still execute (Requirement 12.3)
            errors.Add(new UnknownException(ex));
        }
    }

    private static async Task TryExecuteStrategy(IReadOnlyList<MeetingState> meetings, INotificationStrategy strategy, List<Error> errors)
    {
        // Always execute per-cycle notifications (e.g., beeps, sounds)
        var cycleResult = await strategy.ExecuteOnCycleAsync(meetings);
        cycleResult.OnError(errors.Add);

        // Only execute level-change notifications when level actually changed (e.g., toasts)
        foreach (var meeting in meetings.Where(m => m.NotificationLevelHasChanged))
        {
            var levelChangeResult = await strategy.ExecuteOnLevelChangeAsync(meeting);
            levelChangeResult.OnError(errors.Add);
        }
    }
}
