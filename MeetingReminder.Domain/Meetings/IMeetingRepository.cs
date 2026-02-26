namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Repository abstraction for managing meeting state.
/// Provides thread-safe access to meeting notification states.
/// </summary>
public interface IMeetingRepository
{
    // TODO: Probably want to always include calendar name with the id, like a compound key.
    // separate calendars aren't guaranteed to have non-overlapping id spaces.

    /// <summary>
    /// Gets a meeting state by its ID.
    /// </summary>
    /// <param name="id">The meeting ID to look up</param>
    /// <returns>A Result containing the MeetingState or an error if not found</returns>
    Result<MeetingState, Error> GetById(string id);

    /// <summary>
    /// Gets all meeting states.
    /// </summary>
    /// <returns>A Result containing all meeting states or an error</returns>
    Result<IReadOnlyList<MeetingState>, Error> GetAll();

    Result<IReadOnlyList<MeetingState>, Error> GetAllByCalendar(string calendar);

    Result<MeetingState, Error> Add(MeetingState state);

    Result<MeetingState, Error> Update(MeetingState state);

    Result<string, Error> Remove(string id);
}
