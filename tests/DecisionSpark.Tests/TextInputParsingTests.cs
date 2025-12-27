using DecisionSpark.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DecisionSpark.Tests;

/// <summary>
/// T012: Tests for free-text input parsing with validation history tracking
/// </summary>
public class TextInputParsingTests
{
    private readonly Mock<ILogger<TraitParser>> _mockLogger;
    private readonly Mock<IOpenAIService> _mockOpenAIService;
    private readonly TraitParser _parser;

    public TextInputParsingTests()
    {
        _mockLogger = new Mock<ILogger<TraitParser>>();
        _mockOpenAIService = new Mock<IOpenAIService>();
        _mockOpenAIService.Setup(s => s.IsAvailable()).Returns(false); // Use regex-only parsing
        _parser = new TraitParser(_mockLogger.Object, _mockOpenAIService.Object);
    }

    [Fact]
    public async Task ParseAsync_WithSingleInteger_ShouldExtractValue()
    {
        // Arrange
        var input = "5";
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer", "single number");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ExtractedValue.Should().Be(5);
    }

    [Fact]
    public async Task ParseAsync_WithIntegerInText_ShouldExtractValue()
    {
        // Arrange
        var input = "I have 3 children";
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer", "single number");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ExtractedValue.Should().Be(3);
    }

    [Fact]
    public async Task ParseAsync_WithIntegerList_ShouldExtractAllNumbers()
    {
        // Arrange
        var input = "5 people ages 5-40";
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer_list", "list of numbers");

        // Assert
        result.IsValid.Should().BeTrue();
        var list = result.ExtractedValue as List<int>;
        list.Should().NotBeNull();
        list.Should().HaveCount(3);
        list.Should().Contain(new[] { 5, 5, 40 });
    }

    [Fact]
    public async Task ParseAsync_WithCommaSeparatedList_ShouldExtractAllNumbers()
    {
        // Arrange
        var input = "Ages: 4, 9, 38, 40, 12";
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer_list", "comma-separated list");

        // Assert
        result.IsValid.Should().BeTrue();
        var list = result.ExtractedValue as List<int>;
        list.Should().NotBeNull();
        list.Should().HaveCount(5);
        list.Should().Contain(new[] { 4, 9, 38, 40, 12 });
    }

    [Fact]
    public async Task ParseAsync_WithInvalidInteger_ShouldReturnError()
    {
        // Arrange
        var input = "not a number";
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer", "single number");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithSessionAndValidationHistory_ShouldLogContext()
    {
        // Arrange
        var input = "5";
        var session = new DecisionSpark.Core.Models.Runtime.DecisionSession
        {
            SessionId = "test123",
            ValidationHistory = new List<DecisionSpark.Core.Models.Runtime.ValidationHistoryEntry>
            {
                new DecisionSpark.Core.Models.Runtime.ValidationHistoryEntry
                {
                    TraitKey = "test_trait",
                    Attempt = 1,
                    ErrorReason = "Previous failure"
                }
            }
        };
        
        // Act
        var result = await _parser.ParseAsync(input, "test_trait", "integer", "single number", session);

        // Assert
        result.IsValid.Should().BeTrue();
        // Verify logging was called (validation history context was logged)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("previous validation failures")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
