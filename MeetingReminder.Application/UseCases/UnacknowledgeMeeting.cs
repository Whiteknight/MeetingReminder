using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Command to remove the acknowledgement from a previously acknowledged meeting,
/// allowing notifications to resume on the next notification cycle.
/// </summary>
/// <param name="MeetingId">The ID of the meeting to un-acknowledge</param>
public readonly record struct UnacknowledgeMeetingCommand(MeetingId MeetingId);

/// <summary>
/// Handles meeting un-acknowledgement requests.
/// Resets meeting state to unacknowledged so the notification engine
/// recalculates the appropriate level on the next cycle.
/// </summary>
public class UnacknowledgeMeeting
{
    private readonly IMeetingRepository _meetingRepository;

    public UnacknowledgeMeeting(IMeetingRepository meetingRepository)
    {
        _meetingRepository = meetingRepository;
    }

    /// <summary>
    /// Removes the acknowledgement from the specified meeting.
    /// </summary>
    /// <param name="command">The command containing the meeting ID</param>
    /// <returns>A Result containing the updated MeetingState, or an error</returns>
    public Result<MeetingState, NotificationError> Unacknowledge(UnacknowledgeMeetingCommand command)
    {
        if (!command.MeetingId.IsValid)
            return new NotificationError("Meeting ID is required");

        return _meetingRepository.GetById(command.MeetingId)
            .MapError(e => new NotificationError(e.Message))
            .Bind(UpdateMeeting);
    }

    private Result<MeetingState, NotificationError> UpdateMeeting(MeetingState meetingState)
        => _meetingRepository.Update(meetingState.Unacknowledge())
            .MapError(e => new NotificationError(e.Message));
}
