using MeetingReminder.Domain;
using MeetingReminder.Domain.Meetings;
using System.Collections.Concurrent;

namespace MeetingReminder.Infrastructure.Meetings;

/// <summary>
/// Thread-safe in-memory implementation of IMeetingRepository.
/// Uses ConcurrentDictionary for safe concurrent access from multiple threads.
/// </summary>
public sealed class InMemoryMeetingRepository : IMeetingRepository
{
    private readonly ConcurrentDictionary<string, MeetingState> _meetings = new();

    /// <inheritdoc />
    public Task<Result<MeetingState, Error>> GetByIdAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Task.FromResult<Result<MeetingState, Error>>(new MeetingRepositoryError("Meeting ID cannot be null or empty"));

        if (_meetings.TryGetValue(id, out var state))
            return Task.FromResult<Result<MeetingState, Error>>(state);

        return Task.FromResult<Result<MeetingState, Error>>(
            new MeetingRepositoryError($"Meeting with ID '{id}' not found"));
    }

    /// <inheritdoc />
    public Task<Result<IReadOnlyList<MeetingState>, Error>> GetAllAsync()
    {
        var states = _meetings.Values.ToList().AsReadOnly();
        return Task.FromResult<Result<IReadOnlyList<MeetingState>, Error>>(states);
    }

    /// <inheritdoc />
    public Task<Result<Unit, Error>> UpdateAsync(MeetingState state)
    {
        if (state is null)
            return Task.FromResult<Result<Unit, Error>>(new MeetingRepositoryError("Meeting state cannot be null"));

        _meetings[state.Event.Id] = state;
        return Task.FromResult<Result<Unit, Error>>(Unit.Value);
    }

    /// <summary>
    /// Adds or updates a meeting state in the repository.
    /// This is a convenience method for adding new meetings.
    /// </summary>
    /// <param name="state">The meeting state to add or update</param>
    /// <returns>A Result indicating success or failure</returns>
    public Task<Result<Unit, Error>> AddOrUpdateAsync(MeetingState state)
        => UpdateAsync(state);

    /// <summary>
    /// Removes a meeting state from the repository.
    /// </summary>
    /// <param name="id">The ID of the meeting to remove</param>
    /// <returns>A Result indicating success or failure</returns>
    public Task<Result<Unit, Error>> RemoveAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Task.FromResult<Result<Unit, Error>>(new MeetingRepositoryError("Meeting ID cannot be null or empty"));

        _meetings.TryRemove(id, out _);
        return Task.FromResult<Result<Unit, Error>>(Unit.Value);
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
