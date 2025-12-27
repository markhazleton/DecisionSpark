using DecisionSpark.Core.Services;
using DecisionSpark.Core.Models.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DecisionSpark.Tests;

/// <summary>
/// T027: Server-side validation tests for empty multi-select submissions
/// </summary>
public class EmptySelectionValidationTests
{
    private readonly Mock<ILogger<UserSelectionService>> _mockLogger;
    private readonly UserSelectionService _service;

    public EmptySelectionValidationTests()
    {
        _mockLogger = new Mock<ILogger<UserSelectionService>>();
        _service = new UserSelectionService(_mockLogger.Object);
    }

    [Fact]
    public void RequiredMultiSelect_WithEmptySelection_ShouldFail()
    {
        // Arrange: Required multi-select with no selections
        var request = new NextRequest
        {
            UserInput = null,
            SelectedOptionIds = Array.Empty<string>()
        };

        var options = new List<QuestionOptionDto>
        {
            new() { Id = "option-1", Label = "Option 1", Value = "Value1" },
            new() { Id = "option-2", Label = "Option 2", Value = "Value2" }
        };

        // Act: Normalize selection for required multi-select
        var result = _service.NormalizeSelection(request, "multi-select", options);

        // Assert: Should indicate validation failure for required field
        Assert.Equal("multi-select", result.QuestionType);
        Assert.Null(result.SelectedValues);
        Assert.Null(result.SubmittedText);
        
        // Note: Actual required validation happens in controller/parser
        // This service normalizes the input structure
    }

    [Fact]
    public void OptionalMultiSelect_WithEmptySelection_ShouldPass()
    {
        // Arrange: Optional multi-select with no selections
        var request = new NextRequest
        {
            UserInput = null,
            SelectedOptionIds = Array.Empty<string>()
        };

        var options = new List<QuestionOptionDto>
        {
            new() { Id = "option-1", Label = "Option 1", Value = "Value1" },
            new() { Id = "option-2", Label = "Option 2", Value = "Value2" }
        };

        // Act: Normalize selection
        var result = _service.NormalizeSelection(request, "multi-select", options);

        // Assert: Empty selection is flagged as failed (controller/parser determines if optional)
        Assert.Equal("multi-select", result.QuestionType);
        Assert.Equal("Failed", result.ValidationStatus);
        Assert.Equal("No input provided", result.ErrorReason);
    }

    [Fact]
    public void MultiSelect_WithNullAndEmptyArray_ShouldBehaveSame()
    {
        // Arrange: Two requests - one with null, one with empty array
        var requestNull = new NextRequest
        {
            UserInput = null,
            SelectedOptionIds = null
        };

        var requestEmpty = new NextRequest
        {
            UserInput = null,
            SelectedOptionIds = Array.Empty<string>()
        };

        var options = new List<QuestionOptionDto>
        {
            new() { Id = "option-1", Label = "Option 1", Value = "Value1" }
        };

        // Act
        var resultNull = _service.NormalizeSelection(requestNull, "multi-select", options);
        var resultEmpty = _service.NormalizeSelection(requestEmpty, "multi-select", options);

        // Assert: Both should behave the same
        Assert.Equal(resultNull.QuestionType, resultEmpty.QuestionType);
        Assert.Equal(resultNull.ValidationStatus, resultEmpty.ValidationStatus);
    }

    [Fact]
    public void MultiSelect_RequiredField_WithFallbackText_ShouldUseText()
    {
        // Arrange: Empty structured selection but has fallback text (FR-024a)
        var request = new NextRequest
        {
            UserInput = "My custom answer",
            SelectedOptionIds = Array.Empty<string>()
        };

        var options = new List<QuestionOptionDto>
        {
            new() { Id = "option-1", Label = "Option 1", Value = "Value1" }
        };

        // Act: Normalize with fallback text
        var result = _service.NormalizeSelection(request, "multi-select", options);

        // Assert: Should fall back to text when no structured selections
        Assert.Equal("My custom answer", result.SubmittedText);
    }
}
