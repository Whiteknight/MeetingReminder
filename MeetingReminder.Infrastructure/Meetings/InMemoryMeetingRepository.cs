using System.Collections.Concurrent;
using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;

namespace MeetingReminder.Infrastructure.Meetings;

/// <summary>
/// Thread-safe in-memory implementation of IMeetingRepository.
/// Uses ConcurrentDictionary for safe concurrent access from multiple threads.
/// </summary>
public sealed class InMemoryMeetingRepository : IMeetingRepository
{
    private readonly ConcurrentDictionary<string, MeetingState> _meetings = new();

    /// <inheritdoc />
    public Result<MeetingState, Error> GetById(string id)
    {
        if (string.IsNullOrEmpty(id))
            return new MeetingRepositoryError("Meeting ID cannot be null or empty");

        if (_meetings.TryGetValue(id, out var state))
            return state;

        return new MeetingRepositoryError($"Meeting with ID '{id}' not found");
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<MeetingState>, Error> GetAll()
        => _meetings.Values.ToList().AsReadOnly();

    public Result<IReadOnlyList<MeetingState>, Error> GetAllByCalendar(string calendar)
        => _meetings.Values.Where(ms => ms.Event.CalendarSource == calendar).ToList().AsReadOnly();

    /// <summary>
    /// Adds or updates a meeting state in the repository.
    /// This is a convenience method for adding new meetings.
    /// </summary>
    /// <param name="state">The meeting state to add or update</param>
    /// <returns>A Result indicating success or failure</returns>
    public Result<MeetingState, Error> AddOrUpdate(MeetingState state)
    {
        if (state is null)
            return new MeetingRepositoryError("Meeting state cannot be null");

        _meetings[state.Event.Id] = state;
        return state;
    }

    /// <summary>
    /// Removes a meeting state from the repository.
    /// </summary>
    /// <param name="id">The ID of the meeting to remove</param>
    /// <returns>A Result indicating success or failure</returns>
    public Result<string, Error> Remove(string id)
    {
        if (string.IsNullOrEmpty(id))
            return new MeetingRepositoryError("Meeting ID cannot be null or empty");

        _meetings.TryRemove(id, out _);
        return id;
    }

    /// <summary>
    /// Clears all meeting states from the repository.
    /// </summary>
    public void Clear() => _meetings.Clear();

    /// <summary>
    /// Gets the count of meetings in the repository.
    /// </summary>
    public int Count => _meetings.Count;
}
