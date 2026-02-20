using AwesomeAssertions;
using NUnit.Framework;
using DomainAssert = MeetingReminder.Domain.Assert;

namespace MeetingReminder.Domain.Tests;

/// <summary>
/// Unit tests for Assert guard clause methods.
/// </summary>
[TestFixture]
public sealed class AssertTests
{
    [Test]
    public void NotNull_WhenValueIsNotNull_ReturnsValue()
    {
        // Arrange
        var value = "test";

        // Act
        var result = DomainAssert.NotNull(value);

        // Assert
        result.Should().Be("test");
    }

    [Test]
    public void NotNull_WhenValueIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;

        // Act
        var act = () => DomainAssert.NotNull(value);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void NotNullOrEmpty_WhenStringHasValue_ReturnsString()
    {
        // Arrange
        var value = "test";

        // Act
        var result = DomainAssert.NotNullOrEmpty(value);

        // Assert
        result.Should().Be("test");
    }

    [Test]
    public void NotNullOrEmpty_WhenStringIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        string value = null!;

        // Act
        var act = () => DomainAssert.NotNullOrEmpty(value);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void NotNullOrEmpty_WhenStringIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var value = "";

        // Act
        var act = () => DomainAssert.NotNullOrEmpty(value);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    [Test]
    public void NotZeroOrNegative_WhenPositive_ReturnsValue()
    {
        // Act
        var result = DomainAssert.NotZeroOrNegative(5);

        // Assert
        result.Should().Be(5);
    }

    [Test]
    public void NotZeroOrNegative_WhenZero_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => DomainAssert.NotZeroOrNegative(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void NotZeroOrNegative_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => DomainAssert.NotZeroOrNegative(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void InRange_WhenValueInRange_ReturnsValue()
    {
        // Act
        var result = DomainAssert.InRange(5, 1, 10);

        // Assert
        result.Should().Be(5);
    }

    [Test]
    public void InRange_WhenValueAtMinimum_ReturnsValue()
    {
        // Act
        var result = DomainAssert.InRange(1, 1, 10);

        // Assert
        result.Should().Be(1);
    }

    [Test]
    public void InRange_WhenValueAtMaximum_ReturnsValue()
    {
        // Act
        var result = DomainAssert.InRange(10, 1, 10);

        // Assert
        result.Should().Be(10);
    }

    [Test]
    public void InRange_WhenValueBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => DomainAssert.InRange(0, 1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*must be between 1 and 10*");
    }

    [Test]
    public void InRange_WhenValueAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => DomainAssert.InRange(11, 1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*must be between 1 and 10*");
    }

    [Test]
    public void NotNegative_WhenPositive_ReturnsValue()
    {
        // Act
        var result = DomainAssert.NotNegative(5);

        // Assert
        result.Should().Be(5);
    }

    [Test]
    public void NotNegative_WhenZero_ReturnsValue()
    {
        // Act
        var result = DomainAssert.NotNegative(0);

        // Assert
        result.Should().Be(0);
    }

    [Test]
    public void NotNegative_WhenNegative_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => DomainAssert.NotNegative(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*must not be negative*");
    }
}
