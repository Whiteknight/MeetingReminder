using AwesomeAssertions;
using NUnit.Framework;

namespace MeetingReminder.Domain.Tests;

/// <summary>
/// Unit tests for Result<T, TE1> type.
/// Tests monadic operations and pattern matching.
/// </summary>
[TestFixture]
public sealed class ResultTests
{
    [Test]
    public void ImplicitConversion_FromValue_CreatesSuccessResult()
    {
        // Arrange & Act
        Result<int, string> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsError.Should().BeFalse();
    }

    [Test]
    public void ImplicitConversion_FromError_CreatesErrorResult()
    {
        // Arrange & Act
        Result<int, string> result = "error message";

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
    }

    [Test]
    public void FromValue_CreatesSuccessResult()
    {
        // Act
        var result = Result.FromValue<string, Exception>("success");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void FromError_CreatesErrorResult()
    {
        // Act
        var result = Result.FromError<string, Exception>(new InvalidOperationException("test"));

        // Assert
        result.IsError.Should().BeTrue();
    }

    [Test]
    public void Match_WhenSuccess_CallsOnSuccessFunction()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onError: e => $"Error: {e}");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Test]
    public void Match_WhenError_CallsOnErrorFunction()
    {
        // Arrange
        Result<int, string> result = "something went wrong";

        // Act
        var output = result.Match(
            onSuccess: v => $"Value: {v}",
            onError: e => $"Error: {e}");

        // Assert
        output.Should().Be("Error: something went wrong");
    }

    [Test]
    public void Switch_WhenSuccess_CallsOnSuccessAction()
    {
        // Arrange
        Result<int, string> result = 42;
        var successCalled = false;
        var errorCalled = false;

        // Act
        result.Switch(
            onSuccess: _ => successCalled = true,
            onError: _ => errorCalled = true);

        // Assert
        successCalled.Should().BeTrue();
        errorCalled.Should().BeFalse();
    }

    [Test]
    public void Switch_WhenError_CallsOnErrorAction()
    {
        // Arrange
        Result<int, string> result = "error";
        var successCalled = false;
        var errorCalled = false;

        // Act
        result.Switch(
            onSuccess: _ => successCalled = true,
            onError: _ => errorCalled = true);

        // Assert
        successCalled.Should().BeFalse();
        errorCalled.Should().BeTrue();
    }

    [Test]
    public void Map_WhenSuccess_TransformsValue()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var mapped = result.Map(v => v * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault(0).Should().Be(84);
    }

    [Test]
    public void Map_WhenError_PreservesError()
    {
        // Arrange
        Result<int, string> result = "original error";

        // Act
        var mapped = result.Map(v => v * 2);

        // Assert
        mapped.IsError.Should().BeTrue();
        mapped.GetErrorOrDefault("").Should().Be("original error");
    }

    [Test]
    public void MapError_WhenError_TransformsError()
    {
        // Arrange
        Result<int, string> result = "error";

        // Act
        var mapped = result.MapError(e => $"Wrapped: {e}");

        // Assert
        mapped.IsError.Should().BeTrue();
        mapped.GetErrorOrDefault("").Should().Be("Wrapped: error");
    }

    [Test]
    public void MapError_WhenSuccess_PreservesValue()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var mapped = result.MapError(e => $"Wrapped: {e}");

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.GetValueOrDefault(0).Should().Be(42);
    }

    [Test]
    public void Bind_WhenSuccess_ChainsOperation()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var bound = result.Bind(v => 
            v > 0 
                ? Result.FromValue<string, string>($"Positive: {v}") 
                : Result.FromError<string, string>("Not positive"));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.GetValueOrDefault("").Should().Be("Positive: 42");
    }

    [Test]
    public void Bind_WhenInitialError_DoesNotChain()
    {
        // Arrange
        Result<int, string> result = "initial error";

        // Act
        var bound = result.Bind(v => Result.FromValue<string, string>($"Value: {v}"));

        // Assert
        bound.IsError.Should().BeTrue();
        bound.GetErrorOrDefault("").Should().Be("initial error");
    }

    [Test]
    public void Bind_WhenChainedOperationFails_ReturnsChainedError()
    {
        // Arrange
        Result<int, string> result = -5;

        // Act
        var bound = result.Bind(v => 
            v > 0 
                ? Result.FromValue<string, string>($"Positive: {v}") 
                : Result.FromError<string, string>("Not positive"));

        // Assert
        bound.IsError.Should().BeTrue();
        bound.GetErrorOrDefault("").Should().Be("Not positive");
    }

    [Test]
    public void OnSuccess_WhenSuccess_ExecutesAction()
    {
        // Arrange
        Result<int, string> result = 42;
        var capturedValue = 0;

        // Act
        result.OnSuccess(v => capturedValue = v);

        // Assert
        capturedValue.Should().Be(42);
    }

    [Test]
    public void OnSuccess_WhenError_DoesNotExecuteAction()
    {
        // Arrange
        Result<int, string> result = "error";
        var actionExecuted = false;

        // Act
        result.OnSuccess(_ => actionExecuted = true);

        // Assert
        actionExecuted.Should().BeFalse();
    }

    [Test]
    public void OnError_WhenError_ExecutesAction()
    {
        // Arrange
        Result<int, string> result = "error message";
        var capturedError = "";

        // Act
        result.OnError(e => capturedError = e);

        // Assert
        capturedError.Should().Be("error message");
    }

    [Test]
    public void OnError_WhenSuccess_DoesNotExecuteAction()
    {
        // Arrange
        Result<int, string> result = 42;
        var actionExecuted = false;

        // Act
        result.OnError(_ => actionExecuted = true);

        // Assert
        actionExecuted.Should().BeFalse();
    }

    [Test]
    public void GetValueOrDefault_WhenSuccess_ReturnsValue()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var value = result.GetValueOrDefault(0);

        // Assert
        value.Should().Be(42);
    }

    [Test]
    public void GetValueOrDefault_WhenError_ReturnsDefault()
    {
        // Arrange
        Result<int, string> result = "error";

        // Act
        var value = result.GetValueOrDefault(99);

        // Assert
        value.Should().Be(99);
    }

    [Test]
    public void GetErrorOrDefault_WhenError_ReturnsError()
    {
        // Arrange
        Result<int, string> result = "actual error";

        // Act
        var error = result.GetErrorOrDefault("default error");

        // Assert
        error.Should().Be("actual error");
    }

    [Test]
    public void GetErrorOrDefault_WhenSuccess_ReturnsDefault()
    {
        // Arrange
        Result<int, string> result = 42;

        // Act
        var error = result.GetErrorOrDefault("default error");

        // Assert
        error.Should().Be("default error");
    }

    [Test]
    public void Try_WhenFunctionSucceeds_ReturnsSuccessResult()
    {
        // Act
        var result = Result.Try(() => 42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValueOrDefault(0).Should().Be(42);
    }

    [Test]
    public void Try_WhenFunctionThrows_ReturnsErrorResult()
    {
        // Act
        var result = Result.Try<int>(() => throw new InvalidOperationException("test error"));

        // Assert
        result.IsError.Should().BeTrue();
        result.GetErrorOrDefault(null!).Should().BeOfType<InvalidOperationException>();
    }
}
