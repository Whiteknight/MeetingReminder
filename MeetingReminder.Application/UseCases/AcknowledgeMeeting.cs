using MeetingReminder.Domain;
using MeetingReminder.Domain.Browsers;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Domain.Notifications;

namespace MeetingReminder.Application.UseCases;

/// <summary>
/// Command to acknowledge a meeting, optionally opening its meeting link.
/// </summary>
/// <param name="MeetingId">The ID of the meeting to acknowledge</param>
/// <param name="OpenLink">Whether to open the meeting link in the browser</param>
public readonly record struct AcknowledgeMeetingCommand(MeetingId MeetingId, bool OpenLink);

/// <summary>
/// Handles meeting acknowledgement requests.
/// Marks meetings as acknowledged, optionally opens meeting links.
/// </summary>
public class AcknowledgeMeeting
{
    private readonly IMeetingRepository _meetingRepository;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly ITimeProvider _time;

    public AcknowledgeMeeting(
        IMeetingRepository meetingRepository,
        IBrowserLauncher browserLauncher,
        ITimeProvider time)
    {
        _meetingRepository = meetingRepository;
        _browserLauncher = browserLauncher;
        _time = time;
    }

    /// <summary>
    /// Handles the acknowledge meeting command.
    /// </summary>
    /// <param name="command">The command containing meeting ID and open link flag</param>
    /// <returns>A Result indicating success or failure with error details</returns>
    public async Task<Result<MeetingState, NotificationError>> Acknowledge(AcknowledgeMeetingCommand command)
    {
        if (!command.MeetingId.IsValid)
            return new NotificationError("Meeting ID is required");

        // Get meeting state from repository
        return _meetingRepository.GetById(command.MeetingId)
            .MapError(e => new NotificationError(e.Message))
            .Bind(meeting => TryLaunchBrowser(command, meeting))
            .Bind(meeting => UpdateMeeting(command, meeting));
    }

    private Result<MeetingState, NotificationError> UpdateMeeting(AcknowledgeMeetingCommand command, MeetingState meetingState)
        => _meetingRepository.Update(meetingState.Acknowledge(_time.UtcNow))
            .MapError(e => new NotificationError(e.Message));

    private Result<MeetingState, NotificationError> TryLaunchBrowser(AcknowledgeMeetingCommand command, MeetingState meetingState)
    {
        // Open link if requested and link exists
        if (!command.OpenLink || meetingState.Event.Link is null)
            return meetingState;

        return _browserLauncher.OpenUrl(meetingState.Event.Link.Url)
            .MapError(_ => new NotificationError($"Failed to open meeting link: {meetingState.Event.Link.Url}", "BrowserLauncher"))
            .Map(_ => meetingState);
    }
}
