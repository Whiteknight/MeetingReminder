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

    public AcknowledgeMeeting(
        IMeetingRepository meetingRepository,
        IBrowserLauncher browserLauncher)
    {
        _meetingRepository = meetingRepository;
        _browserLauncher = browserLauncher;
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

        var meetingState = meetingResult.Match(s => s, _ => null!);

        // Open link if requested and link exists
        var linkOpened = false;
        if (command.OpenLink && meetingState.Event.Link is not null)
        {
            var launchResult = _browserLauncher.OpenUrl(meetingState.Event.Link.Url);

            if (launchResult.IsError)
            {
                return new NotificationError(
                    $"Failed to open meeting link: {meetingState.Event.Link.Url}",
                    StrategyName: "BrowserLauncher");
            }

            linkOpened = true;
        }

        // Acknowledge the meeting
        meetingState.Acknowledge();

        // Update the repository
        var updateResult = _meetingRepository.AddOrUpdate(meetingState);
        if (updateResult.IsError)
            return new NotificationError($"Failed to update meeting state for {command.MeetingId}");

        return Unit.Value;
    }
}
