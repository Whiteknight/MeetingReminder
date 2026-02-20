using AwesomeAssertions;
using NUnit.Framework;

namespace MeetingReminder.Domain.Tests;

/// <summary>
/// Unit tests for Error types.
/// Tests AggregateError message building logic.
/// </summary>
[TestFixture]
public sealed class ErrorTests
{
    [Test]
    public void AggregateError_WhenNoErrors_ReturnsDefaultMessage()
    {
        // Arrange
        var errors = Array.Empty<Error>();

        // Act
        var aggregate = new AggregateError(errors);

        // Assert
        aggregate.Message.Should().Be("Error not specified");
    }

    [Test]
    public void AggregateError_WhenSingleError_ReturnsSingleErrorMessage()
    {
        // Arrange
        var errors = new Error[] { new UnknownError("Single error") };

        // Act
        var aggregate = new AggregateError(errors);

        // Assert
        aggregate.Message.Should().Be("Unknown error: Single error");
    }

    [Test]
    public void AggregateError_WhenMultipleErrors_ReturnsCombinedMessage()
    {
        // Arrange
        var errors = new Error[]
        {
            new UnknownError("First error"),
            new UnknownError("Second error"),
            new UnknownError("Third error")
        };

        // Act
        var aggregate = new AggregateError(errors);

        // Assert
        aggregate.Message.Should().Contain("3 errors:");
        aggregate.Message.Should().Contain("Unknown error: First error");
        aggregate.Message.Should().Contain("Unknown error: Second error");
        aggregate.Message.Should().Contain("Unknown error: Third error");
    }

    [Test]
    public void UnknownException_ContainsExceptionTypeAndMessage()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception message");

        // Act
        var error = new UnknownException(exception);

        // Assert
        error.Message.Should().Contain("InvalidOperationException");
        error.Message.Should().Contain("Test exception message");
    }

    [Test]
    public void UnknownError_PrefixesMessageWithUnknownError()
    {
        // Act
        var error = new UnknownError("Something went wrong");

        // Assert
        error.Message.Should().Be("Unknown error: Something went wrong");
    }
}
