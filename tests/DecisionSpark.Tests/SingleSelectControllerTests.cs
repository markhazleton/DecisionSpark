using DecisionSpark.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DecisionSpark.Tests;

/// <summary>
/// T019: Tests for single-select option handling
/// </summary>
public class SingleSelectControllerTests
{
    [Fact]
    public void OptionIdGenerator_ShouldGenerateStableIds()
    {
        // Arrange
        var logger = new Mock<ILogger<OptionIdGenerator>>();
        var generator = new OptionIdGenerator(logger.Object);

        // Act
        var id1 = generator.GenerateId("Outdoor Activities");
        var id2 = generator.GenerateId("Outdoor Activities");
        var id3 = generator.GenerateId("outdoor-activities");

        // Assert
        id1.Should().Be("outdoor-activities");
        id2.Should().Be("outdoor-activities"); // Deterministic
        id3.Should().Be("outdoor-activities"); // Same result
    }

    [Fact]
    public void OptionIdGenerator_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var logger = new Mock<ILogger<OptionIdGenerator>>();
        var generator = new OptionIdGenerator(logger.Object);

        // Act
        var id = generator.GenerateId("Board Games & Puzzles!");

        // Assert
        id.Should().Be("board-games-puzzles");
    }

    [Fact]
    public void OptionIdGenerator_ShouldHandleSpaces()
    {
        // Arrange
        var logger = new Mock<ILogger<OptionIdGenerator>>();
        var generator = new OptionIdGenerator(logger.Object);

        // Act
        var id = generator.GenerateId("Watch a movie");

        // Assert
        id.Should().Be("watch-a-movie");
    }

    [Fact]
    public void OptionIdGenerator_ShouldHandleEmptyInput()
    {
        // Arrange
        var logger = new Mock<ILogger<OptionIdGenerator>>();
        var generator = new OptionIdGenerator(logger.Object);

        // Act
        var id = generator.GenerateId("");

        // Assert
        id.Should().Be("unknown");
    }

    [Fact]
    public void OptionIdGenerator_ShouldCollapseMultipleHyphens()
    {
        // Arrange
        var logger = new Mock<ILogger<OptionIdGenerator>>();
        var generator = new OptionIdGenerator(logger.Object);

        // Act
        var id = generator.GenerateId("Item---with----many----hyphens");

        // Assert
        id.Should().Be("item-with-many-hyphens");
    }
}
