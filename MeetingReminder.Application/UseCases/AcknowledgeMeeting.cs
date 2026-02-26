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
public readonly record struct AcknowledgeMeetingCommand(string MeetingId, bool OpenLink);

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
    public async Task<Result<Unit, NotificationError>> Acknowledge(AcknowledgeMeetingCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.MeetingId))
            return new NotificationError("Meeting ID is required");

        // Get meeting state from repository
        var meetingResult = _meetingRepository.GetById(command.MeetingId);
        if (meetingResult.IsError)
            return new NotificationError($"Meeting {command.MeetingId} not found");

        // TODO: Bind this to avoid the IsError/GetValueOrDefault pattern
        var meetingState = meetingResult.GetValueOrDefault(default);

        // Open link if requested and link exists
        if (command.OpenLink && meetingState.Event.Link is not null)
        {
            var launchResult = _browserLauncher.OpenUrl(meetingState.Event.Link.Url);

            if (launchResult.IsError)
            {
                return new NotificationError(
                    $"Failed to open meeting link: {meetingState.Event.Link.Url}",
                    StrategyName: "BrowserLauncher");
            }
        }

        // Acknowledge the meeting
        meetingState = meetingState.Acknowledge(_time.UtcNow);

        // Update the repository
        var updateResult = _meetingRepository.Update(meetingState);
        if (updateResult.IsError)
            return new NotificationError($"Failed to update meeting state for {command.MeetingId}");

        return Unit.Value;
    }
}
