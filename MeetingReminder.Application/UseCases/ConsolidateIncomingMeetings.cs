using MeetingReminder.Domain;
using MeetingReminder.Domain.Calendars;
using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Application.UseCases;

public sealed class ConsolidateIncomingMeetings
{
    private readonly IMeetingRepository _meetings;

    public ConsolidateIncomingMeetings(IMeetingRepository meetings)
    {
        _meetings = meetings;
    }

    public async Task<Result<Unit, Error>> Consolidate(
        IReadOnlyDictionary<CalendarName, IReadOnlyList<MeetingEvent>> events,
        CancellationToken cancellationToken)
    {
        // TODO: There's a concurrency issue here where we call .GetAllByCalendar() and then act
        // on that list making updates to the repo, but other threads could be modifying these
        // items at the same time. Some kind of synchonization or event-queueing would help.
        var errors = new List<Error>();
        foreach (var (calendarSource, incomingMeetings) in events)
        {
            _meetings.GetAllByCalendar(calendarSource)
                .Bind(existing => ConsolidateSingleSource(existing, incomingMeetings))
                .Bind(RemoveRemainingItems)
                .OnError(errors.Add);
        }

        return errors.Count > 0
            ? Error.Flatten(errors)
            : Unit.Value;
    }

    private Result<Dictionary<MeetingId, MeetingState>, Error> ConsolidateSingleSource(IReadOnlyList<MeetingState> existingList, IReadOnlyList<MeetingEvent> incomingMeetings)
    {
        var errors = new List<Error>();
        var existing = existingList.ToDictionary(e => e.Event.Id);
        foreach (var incoming in incomingMeetings)
        {
            // TODO: .Add() and .Update() can both return errors in theory. We should handle those.
            if (!existing.ContainsKey(incoming.Id))
            {
                _meetings.Add(MeetingState.New(incoming))
                    .OnError(errors.Add);
                continue;
            }

            _meetings.Update(existing[incoming.Id].UpdateEvent(incoming))
                .OnError(errors.Add);
            existing.Remove(incoming.Id);
        }
        return errors.Count > 0
            ? Error.Flatten(errors)
            : existing;
    }

    private Result<Dictionary<MeetingId, MeetingState>, Error> RemoveRemainingItems(Dictionary<MeetingId, MeetingState> existing)
    {
        var errors = new List<Error>();
        foreach (var remaining in existing.Values)
            _meetings.Remove(remaining.Event.Id).OnError(errors.Add);
        return errors.Count > 0
            ? Error.Flatten(errors)
            : existing;
    }
}
