using MeetingReminder.Domain;
using MeetingReminder.Domain.Configuration;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Application.UseCases;

public sealed class UpdateAllNotificationLevels
{
    private ITimeProvider _timeProvider;
    private IMeetingRepository _meetings;
    private CalculateNotificationLevel _calculateNotificationLevel;
    private IAppConfiguration _config;

    public UpdateAllNotificationLevels(ITimeProvider timeProvider, IMeetingRepository meetings, CalculateNotificationLevel calculateNotificationLevel, IAppConfiguration config)
    {
        _timeProvider = timeProvider;
        _meetings = meetings;
        _calculateNotificationLevel = calculateNotificationLevel;
        _config = config;
    }

    public IReadOnlyList<MeetingState> UpdateAndReturnNotifiableMeetings()
    {
        var currentTime = _timeProvider.UtcNow;

        var meetings = _meetings.GetAll().Map(r => r.ToArray()).GetValueOrDefault([]);

        for (int i = 0; i < meetings.Length; i++)
        {
            var state = meetings[i];
            if (state.IsAcknowledged)
                continue;

            // Get calendar-specific notification rules
            var rules = _config.GetCalendarNotificationRules(state.Event.Calendar);
            var newLevel = _calculateNotificationLevel.Calculate(new CalculateNotificationLevelQuery(
                Meeting: state.Event,
                CurrentTime: currentTime,
                Thresholds: _config.Thresholds,
                Rules: rules));

            // Update notification level (only escalates, never decreases - Requirement 8.5)
            meetings[i] = state.UpdateNotificationLevel(newLevel, _timeProvider.UtcNow);
            _meetings.Update(meetings[i]);
        }

        return meetings
            .Where(s => !s.IsAcknowledged && s.CurrentLevel != NotificationLevel.None)
            .ToList();
    }
}
