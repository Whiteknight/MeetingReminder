namespace MeetingReminder.Domain.Meetings;

/// <summary>
/// Repository abstraction for managing meeting state.
/// Provides thread-safe access to meeting notification states.
/// </summary>
public interface IMeetingRepository
{
    /// <summary>
    /// Gets a meeting state by its ID.
    /// </summary>
    /// <param name="id">The meeting ID to look up</param>
    /// <returns>A Result containing the MeetingState or an error if not found</returns>
    Task<Result<MeetingState, Error>> GetByIdAsync(string id);

    /// <summary>
    /// Gets all meeting states.
    /// </summary>
    /// <returns>A Result containing all meeting states or an error</returns>
    Task<Result<IReadOnlyList<MeetingState>, Error>> GetAllAsync();

    Task<Result<Unit, Error>> AddOrUpdateAsync(MeetingState state);

    /// <summary>
    /// Updates a meeting state.
    /// </summary>
    /// <param name="state">The meeting state to update</param>
    /// <returns>A Result indicating success or failure</returns>
    Task<Result<Unit, Error>> UpdateAsync(MeetingState state);

    Task<Result<Unit, Error>> RemoveAsync(string id);
}
