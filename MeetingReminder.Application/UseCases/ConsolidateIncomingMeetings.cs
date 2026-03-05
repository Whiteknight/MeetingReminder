using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Application.UseCases;

public sealed class ConsolidateIncomingMeetings
{
    private IMeetingRepository _meetings;

    public ConsolidateIncomingMeetings(IMeetingRepository meetings)
    {
        _meetings = meetings;
    }

    public async Task Consolidate(
        IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>> events,
        CancellationToken cancellationToken)
    {
        foreach (var (calendarSource, incomingMeetings) in events)
        {
            var existingResult = _meetings.GetAllByCalendar(calendarSource);
            if (!existingResult.IsSuccess)
                continue;
            var existing = existingResult.GetValueOrDefault([]).ToDictionary(e => e.Event.Id);

            foreach (var incoming in incomingMeetings)
            {
                if (!existing.ContainsKey(incoming.Id))
                {
                    _meetings.Add(MeetingState.New(incoming));
                    continue;
                }

                _meetings.Update(existing[incoming.Id].UpdateEvent(incoming));
                existing.Remove(incoming.Id);
            }

            foreach (var remaining in existing.Values)
                _meetings.Remove(remaining.Event.Id);
        }
    }
}
