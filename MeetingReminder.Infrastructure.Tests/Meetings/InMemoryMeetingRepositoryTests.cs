using AwesomeAssertions;
using MeetingReminder.Domain.Meetings;
using MeetingReminder.Infrastructure.Meetings;
using NUnit.Framework;

namespace MeetingReminder.Infrastructure.Tests.Meetings;

/// <summary>
/// Unit tests for InMemoryMeetingRepository covering:
/// - Thread-safe storage using ConcurrentDictionary
/// - GetByIdAsync, GetAllAsync, UpdateAsync operations
/// - Error handling for invalid inputs
/// </summary>
[TestFixture]
public sealed class InMemoryMeetingRepositoryTests
{
    private InMemoryMeetingRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryMeetingRepository();
    }

    private static MeetingEvent CreateTestMeeting(string id, string title = "Test Meeting")
    {
        return new MeetingEvent(
            id: id,
            title: title,
            startTime: DateTime.UtcNow.AddHours(1),
            endTime: DateTime.UtcNow.AddHours(2),
            description: "Test description",
            location: "Test location",
            isAllDay: false,
            calendarSource: "TestCalendar");
    }

    [Test]
    public async Task GetByIdAsync_WhenMeetingExists_ReturnsMeetingState()
    {
        // Arrange
        var meeting = CreateTestMeeting("meeting-1");
        var state = new MeetingState(meeting);
        await _repository.UpdateAsync(state);

        // Act
        var result = await _repository.GetByIdAsync("meeting-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            retrieved => retrieved.Event.Id.Should().Be("meeting-1"),
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public async Task GetByIdAsync_WhenMeetingDoesNotExist_ReturnsError()
    {
        // Act
        var result = await _repository.GetByIdAsync("nonexistent-id");

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            state => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("not found"));
    }

    [Test]
    public async Task GetByIdAsync_WhenIdIsNull_ReturnsError()
    {
        // Act
        var result = await _repository.GetByIdAsync(null!);

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            state => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("cannot be null or empty"));
    }

    [Test]
    public async Task GetByIdAsync_WhenIdIsEmpty_ReturnsError()
    {
        // Act
        var result = await _repository.GetByIdAsync(string.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            state => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("cannot be null or empty"));
    }

    [Test]
    public async Task GetAllAsync_WhenRepositoryIsEmpty_ReturnsEmptyList()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            states => states.Should().BeEmpty(),
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public async Task GetAllAsync_WhenRepositoryHasMeetings_ReturnsAllMeetings()
    {
        // Arrange
        var meeting1 = CreateTestMeeting("meeting-1", "Meeting 1");
        var meeting2 = CreateTestMeeting("meeting-2", "Meeting 2");
        var meeting3 = CreateTestMeeting("meeting-3", "Meeting 3");

        await _repository.UpdateAsync(new MeetingState(meeting1));
        await _repository.UpdateAsync(new MeetingState(meeting2));
        await _repository.UpdateAsync(new MeetingState(meeting3));

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Switch(
            states =>
            {
                states.Should().HaveCount(3);
                states.Select(s => s.Event.Id).Should().Contain("meeting-1");
                states.Select(s => s.Event.Id).Should().Contain("meeting-2");
                states.Select(s => s.Event.Id).Should().Contain("meeting-3");
            },
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public async Task UpdateAsync_WhenStateIsValid_AddsToRepository()
    {
        // Arrange
        var meeting = CreateTestMeeting("meeting-1");
        var state = new MeetingState(meeting);

        // Act
        var result = await _repository.UpdateAsync(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repository.Count.Should().Be(1);
    }

    [Test]
    public async Task UpdateAsync_WhenMeetingAlreadyExists_UpdatesExistingState()
    {
        // Arrange
        var meeting = CreateTestMeeting("meeting-1");
        var originalState = new MeetingState(meeting);
        await _repository.UpdateAsync(originalState);

        var updatedState = new MeetingState(meeting);
        updatedState.Acknowledge();

        // Act
        var result = await _repository.UpdateAsync(updatedState);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repository.Count.Should().Be(1);

        var retrievedResult = await _repository.GetByIdAsync("meeting-1");
        retrievedResult.Switch(
            retrieved => retrieved.IsAcknowledged.Should().BeTrue(),
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public async Task UpdateAsync_WhenStateIsNull_ReturnsError()
    {
        // Act
        var result = await _repository.UpdateAsync(null!);

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            unit => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("cannot be null"));
    }

    [Test]
    public async Task RemoveAsync_WhenMeetingExists_RemovesFromRepository()
    {
        // Arrange
        var meeting = CreateTestMeeting("meeting-1");
        await _repository.UpdateAsync(new MeetingState(meeting));

        // Act
        var result = await _repository.RemoveAsync("meeting-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repository.Count.Should().Be(0);
    }

    [Test]
    public async Task RemoveAsync_WhenMeetingDoesNotExist_ReturnsSuccess()
    {
        // Act - removing non-existent meeting should succeed (idempotent)
        var result = await _repository.RemoveAsync("nonexistent-id");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public async Task RemoveAsync_WhenIdIsNull_ReturnsError()
    {
        // Act
        var result = await _repository.RemoveAsync(null!);

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            unit => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("cannot be null or empty"));
    }

    [Test]
    public async Task RemoveAsync_WhenIdIsEmpty_ReturnsError()
    {
        // Act
        var result = await _repository.RemoveAsync(string.Empty);

        // Assert
        result.IsError.Should().BeTrue();
        result.Switch(
            unit => throw new AssertionException("Expected error but got success"),
            error => error.Message.Should().Contain("cannot be null or empty"));
    }

    [Test]
    public async Task Clear_RemovesAllMeetings()
    {
        // Arrange
        await _repository.UpdateAsync(new MeetingState(CreateTestMeeting("meeting-1")));
        await _repository.UpdateAsync(new MeetingState(CreateTestMeeting("meeting-2")));
        await _repository.UpdateAsync(new MeetingState(CreateTestMeeting("meeting-3")));

        // Act
        _repository.Clear();

        // Assert
        _repository.Count.Should().Be(0);
        var result = await _repository.GetAllAsync();
        result.Switch(
            states => states.Should().BeEmpty(),
            error => throw new AssertionException($"Expected success but got error: {error.Message}"));
    }

    [Test]
    public async Task ConcurrentOperations_AreThreadSafe()
    {
        // Arrange
        const int operationCount = 100;
        var tasks = new List<Task>();

        // Act - perform concurrent adds, reads, and removes
        for (int i = 0; i < operationCount; i++)
        {
            var id = $"meeting-{i}";
            tasks.Add(Task.Run(async () =>
            {
                var meeting = CreateTestMeeting(id);
                await _repository.UpdateAsync(new MeetingState(meeting));
                await _repository.GetByIdAsync(id);
                await _repository.GetAllAsync();
            }));
        }

        // Assert - should complete without exceptions
        await Task.WhenAll(tasks);
        _repository.Count.Should().Be(operationCount);
    }
}
