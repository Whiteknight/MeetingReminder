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
            .Where(s => config.EnabledNotificationStrategies.Contains(s.StrategyName, StringComparer.OrdinalIgnoreCase))
            .Where(s => s.IsSupported)
            .ToList();
    }

    public async Task<Result<Unit, Error>> Notify(IReadOnlyList<MeetingState> meetings)
    {
        var errors = new List<Error>();
        foreach (var strategy in _enabledStrategies)
        {
            try
            {
                var strategyErrors = await TryExecuteStrategy(meetings, strategy);
                strategyErrors.OnError(errors.Add);
            }
            catch (Exception ex)
            {
                // Catch any unexpected exceptions to ensure other strategies still execute (Requirement 12.3)
                errors.Add(new UnknownException(ex));
            }
        }
        return errors.Count == 0
            ? Unit.Value
            : Error.Flatten(errors);
    }

    private async Task<Result<Unit, Error>> TryExecuteStrategy(IReadOnlyList<MeetingState> meetings, INotificationStrategy strategy)
    {
        var errors = new List<Error>();
        // Always execute per-cycle notifications (e.g., beeps, sounds)
        var cycleResult = await strategy.ExecuteOnCycleAsync(meetings);
        cycleResult.OnError(errors.Add);

        // Only execute level-change notifications when level actually changed (e.g., toasts)
        foreach (var meeting in meetings.Where(m => m.NotificationLevelHasChanged))
        {
            var levelChangeResult = await strategy.ExecuteOnLevelChangeAsync(meeting);
            levelChangeResult.OnError(errors.Add);
        }

        return errors.Count == 0
            ? Unit.Value
            : new AggregateError(errors);
    }
}
