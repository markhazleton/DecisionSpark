using DecisionSpark.Core.Models.Api;
using DecisionSpark.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace DecisionSpark.Tests;

/// <summary>
/// T025: Tests for multi-select handling with option limits
/// </summary>
public class MultiSelectControllerTests
{
    [Fact]
    public void UserSelectionService_WithMultipleOptions_ShouldNormalize()
    {
        // Arrange
        var logger = new Mock<ILogger<UserSelectionService>>();
        var service = new UserSelectionService(logger.Object);
        var request = new NextRequest
        {
            SelectedOptionIds = new[] { "option-1", "option-2", "option-3" }
        };
        var availableOptions = new List<QuestionOptionDto>
        {
            new QuestionOptionDto { Id = "option-1", Value = "VALUE1" },
            new QuestionOptionDto { Id = "option-2", Value = "VALUE2" },
            new QuestionOptionDto { Id = "option-3", Value = "VALUE3" }
        };

        // Act
        var result = service.NormalizeSelection(request, "multi-select", availableOptions);

        // Assert
        result.SelectedOptionIds.Should().HaveCount(3);
        result.SelectedValues.Should().Contain(new[] { "VALUE1", "VALUE2", "VALUE3" });
        result.ValidationStatus.Should().Be("Passed");
    }

    [Fact]
    public void UserSelectionService_WithMoreThan7Options_ShouldLimit()
    {
        // Arrange
        var logger = new Mock<ILogger<UserSelectionService>>();
        var service = new UserSelectionService(logger.Object);
        var request = new NextRequest
        {
            SelectedOptionIds = new[] { "opt1", "opt2", "opt3", "opt4", "opt5", "opt6", "opt7", "opt8", "opt9" }
        };

        // Act
        var result = service.NormalizeSelection(request, "multi-select");

        // Assert
        result.SelectedOptionIds.Should().HaveCount(7); // Limited to 7
    }

    [Fact]
    public void UserSelectionService_WithEmptySelection_ShouldFail()
    {
        // Arrange
        var logger = new Mock<ILogger<UserSelectionService>>();
        var service = new UserSelectionService(logger.Object);
        var request = new NextRequest
        {
            SelectedOptionIds = Array.Empty<string>(),
            UserInput = null
        };

        // Act
        var result = service.NormalizeSelection(request, "multi-select");

        // Assert
        result.ValidationStatus.Should().Be("Failed");
        result.ErrorReason.Should().Be("No input provided");
    }

    [Fact]
    public void UserSelectionService_StructuredOverridesText_ShouldIgnoreUserInput()
    {
        // Arrange
        var logger = new Mock<ILogger<UserSelectionService>>();
        var service = new UserSelectionService(logger.Object);
        var request = new NextRequest
        {
            SelectedOptionIds = new[] { "option-1" },
            UserInput = "This should be ignored"
        };

        // Act
        var result = service.NormalizeSelection(request, "multi-select");

        // Assert
        result.SelectedOptionIds.Should().HaveCount(1);
        result.SubmittedText.Should().BeNull(); // Text was ignored
    }
}
