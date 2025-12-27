using DecisionSpark.Controllers;
using DecisionSpark.Models.Api.DecisionSpecs;
using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DecisionSpark.Tests.Core;

/// <summary>
/// Tests for lifecycle transition validation and enforcement.
/// Task T038: Extend API/repository tests to cover approval requirements, invalid transitions, and regression cases.
/// </summary>
public class LifecycleTransitionTests
{
    private readonly Mock<IDecisionSpecRepository> _mockRepository;
    private readonly Mock<QuestionPatchService> _mockPatchService;
    private readonly Mock<ILogger<DecisionSpecsApiController>> _mockLogger;
    private readonly DecisionSpecsApiController _controller;

    public LifecycleTransitionTests()
    {
        _mockRepository = new Mock<IDecisionSpecRepository>();
        _mockLogger = new Mock<ILogger<DecisionSpecsApiController>>();
        
        var patchServiceLogger = new Mock<ILogger<QuestionPatchService>>();
        _mockPatchService = new Mock<QuestionPatchService>(_mockRepository.Object, patchServiceLogger.Object);

        _controller = new DecisionSpecsApiController(
            _mockRepository.Object,
            _mockPatchService.Object,
            _mockLogger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    #region Valid Transitions

    [Theory]
    [InlineData("Draft", "InReview")]
    [InlineData("Draft", "Retired")]
    [InlineData("InReview", "Draft")]
    [InlineData("InReview", "Published")]
    [InlineData("Published", "InReview")]
    [InlineData("Published", "Retired")]
    public async Task TransitionStatus_AllowsValidTransitions(string currentStatus, string newStatus)
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", currentStatus);
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = newStatus };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.UpdateAsync(
            "test-spec",
            It.Is<DecisionSpecDocument>(d => d.Status == newStatus),
            etag,
            default), Times.Once);
    }

    #endregion

    #region Invalid Transitions

    [Theory]
    [InlineData("Draft", "Published")]
    [InlineData("Published", "Draft")]
    [InlineData("Retired", "Draft")]
    [InlineData("Retired", "InReview")]
    [InlineData("Retired", "Published")]
    [InlineData("InReview", "Retired")]
    public async Task TransitionStatus_RejectsInvalidTransitions(string currentStatus, string newStatus)
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", currentStatus);
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        var request = new StatusTransitionRequest { NewStatus = newStatus };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Invalid status transition");
        problemDetails.Errors.Should().ContainKey("newStatus");
        problemDetails.Errors["newStatus"].Should().Contain($"Cannot transition from {currentStatus} to {newStatus}");
    }

    #endregion

    #region ETag Validation

    [Fact]
    public async Task TransitionStatus_ReturnsBadRequest_WhenIfMatchMissing()
    {
        // Arrange
        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, "");

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors.Should().ContainKey("If-Match");
    }

    [Fact]
    public async Task TransitionStatus_ReturnsConflict_WhenETagMismatch()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "InReview");
        var correctEtag = "correct-etag";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, correctEtag));

        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act - Pass wrong ETag
        var result = await _controller.TransitionStatus("test-spec", request, "wrong-etag");

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problemDetails = conflictResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Concurrency conflict");
    }

    #endregion

    #region Not Found Cases

    [Fact]
    public async Task TransitionStatus_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAsync("nonexistent", null, default))
            .ReturnsAsync((ValueTuple<DecisionSpecDocument, string>?)null);

        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act
        var result = await _controller.TransitionStatus("nonexistent", request, "etag");

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Approval Workflow Tests

    [Fact]
    public async Task TransitionStatus_DraftToInReview_UpdatesMetadata()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "etag-123";
        var originalUpdatedAt = doc.Metadata.UpdatedAt;

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        DecisionSpecDocument? capturedDoc = null;
        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .Callback<string, DecisionSpecDocument, string, CancellationToken>((_, d, _, _) => capturedDoc = d)
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = "InReview" };

        // Act
        await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        capturedDoc.Should().NotBeNull();
        capturedDoc!.Status.Should().Be("InReview");
        capturedDoc.Metadata.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task TransitionStatus_InReviewToPublished_CompletesApprovalFlow()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "InReview");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        _mockRepository.Verify(r => r.UpdateAsync(
            "test-spec",
            It.Is<DecisionSpecDocument>(d => d.Status == "Published"),
            etag,
            default), Times.Once);
    }

    [Fact]
    public async Task TransitionStatus_InReviewToDraft_AllowsChangesRequest()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "InReview");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = "Draft" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_PublishedToRetired_ArchivesSpec()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Published");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = "Retired" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Regression Tests

    [Fact]
    public async Task TransitionStatus_PreventsSameStatusTransition()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        var request = new StatusTransitionRequest { NewStatus = "Draft" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors["newStatus"].Should().Contain("Cannot transition from Draft to Draft");
    }

    [Fact]
    public async Task TransitionStatus_HandlesNullOrEmptyNewStatus()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        var request = new StatusTransitionRequest { NewStatus = "" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_PreservesQuestionDataDuringTransition()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "InReview");
        doc.Questions.Add(new Question
        {
            QuestionId = "q2",
            Type = "MultiSelect",
            Prompt = "Additional question",
            Options = new List<Option>
            {
                new() { OptionId = "o3", Label = "Option 3", Value = "opt3" }
            }
        });
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        DecisionSpecDocument? capturedDoc = null;
        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .Callback<string, DecisionSpecDocument, string, CancellationToken>((_, d, _, _) => capturedDoc = d)
            .ReturnsAsync((doc, "new-etag"));

        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act
        await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        capturedDoc.Should().NotBeNull();
        capturedDoc!.Questions.Should().HaveCount(2);
        capturedDoc.Questions[1].QuestionId.Should().Be("q2");
    }

    [Fact]
    public async Task TransitionStatus_UpdatesRepositoryWithNewETag()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var oldEtag = "old-etag";
        var newEtag = "new-etag";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, oldEtag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), oldEtag, default))
            .ReturnsAsync((doc, newEtag));

        var request = new StatusTransitionRequest { NewStatus = "InReview" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, oldEtag);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        _controller.Response.Headers["ETag"].ToString().Should().Be(newEtag);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("draft")]
    [InlineData("PUBLISHED")]
    [InlineData("inReview")]
    public async Task TransitionStatus_IsCaseSensitive(string invalidCaseStatus)
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        var request = new StatusTransitionRequest { NewStatus = invalidCaseStatus };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        // The transition should fail because the status doesn't match exactly
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TransitionStatus_HandlesRepositoryUpdateFailure()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "InReview");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), etag, default))
            .ThrowsAsync(new InvalidOperationException("ETag mismatch - concurrent modification detected"));

        var request = new StatusTransitionRequest { NewStatus = "Published" };

        // Act
        var result = await _controller.TransitionStatus("test-spec", request, etag);

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problemDetails = conflictResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Status.Should().Be(409);
    }

    #endregion

    #region Helper Methods

    private static DecisionSpecDocument CreateSampleDocument(string specId, string version, string status)
    {
        return new DecisionSpecDocument
        {
            SpecId = specId,
            Version = version,
            Status = status,
            Metadata = new DecisionSpecMetadata
            {
                Name = "Test Spec",
                Description = "A test specification",
                Tags = new List<string> { "test" },
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            },
            Questions = new List<Question>
            {
                new()
                {
                    QuestionId = "q1",
                    Type = "SingleSelect",
                    Prompt = "Test question?",
                    Required = true,
                    Options = new List<Option>
                    {
                        new() { OptionId = "o1", Label = "Option 1", Value = "opt1" }
                    }
                }
            },
            Outcomes = new List<Outcome>
            {
                new()
                {
                    OutcomeId = "outcome1",
                    SelectionRules = new List<string> { "q1:opt1" },
                    DisplayCards = new List<OutcomeDisplayCard>()
                }
            }
        };
    }

    #endregion
}
