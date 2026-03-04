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
    private readonly ConcurrentDictionary<MeetingId, MeetingState> _meetings = new();
    private readonly IChangeNotifier _notifier;

    public InMemoryMeetingRepository(IChangeNotifier notifier)
    {
        _notifier = notifier;
    }

    /// <inheritdoc />
    public Result<MeetingState, Error> GetById(MeetingId id)
    {
        if (!id.IsValid)
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
    public Result<MeetingState, Error> Add(MeetingState state)
    {
        if (state.Event is null)
            return new MeetingRepositoryError("Meeting state event cannot be null");

        Result<MeetingState, Error> result = _meetings.TryAdd(state.Event.Id, state)
            ? state
            : new MeetingRepositoryError($"Meeting with ID '{state.Event.Id}' already exists");

        _notifier?.Set();
        return result;
    }

    public Result<MeetingState, Error> Update(MeetingState state)
    {
        if (state.Event is null)
            return new MeetingRepositoryError("Meeting state event cannot be null");
        var result = _meetings.AddOrUpdate(state.Event.Id, state, (_, _) => state);
        _notifier?.Set();
        return result;
    }

    /// <summary>
    /// Removes a meeting state from the repository.
    /// </summary>
    /// <param name="id">The ID of the meeting to remove</param>
    /// <returns>A Result indicating success or failure</returns>
    public Result<MeetingId, Error> Remove(MeetingId id)
    {
        if (!id.IsValid)
            return new MeetingRepositoryError("Meeting ID cannot be null or empty");

        _meetings.TryRemove(id, out _);
        _notifier?.Set();
        return id;
    }
}
