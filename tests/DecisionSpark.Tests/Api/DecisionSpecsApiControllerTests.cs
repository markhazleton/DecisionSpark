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

namespace DecisionSpark.Tests.Api;

/// <summary>
/// Integration tests for DecisionSpecsApiController covering CRUD, partial updates, soft delete, restore, and concurrency.
/// Task T018: Author integration tests covering CRUD, partial updates, soft delete, restore, and concurrency.
/// </summary>
public class DecisionSpecsApiControllerTests
{
    private readonly Mock<IDecisionSpecRepository> _mockRepository;
    private readonly Mock<TraitPatchService> _mockPatchService;
    private readonly Mock<ILogger<DecisionSpecsApiController>> _mockLogger;
    private readonly DecisionSpecsApiController _controller;

    public DecisionSpecsApiControllerTests()
    {
        _mockRepository = new Mock<IDecisionSpecRepository>();
        _mockLogger = new Mock<ILogger<DecisionSpecsApiController>>();
        
        // TraitPatchService requires repository and logger
        var patchServiceLogger = new Mock<ILogger<TraitPatchService>>();
        _mockPatchService = new Mock<TraitPatchService>(_mockRepository.Object, patchServiceLogger.Object);

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

    #region List Tests

    [Fact]
    public async Task List_ReturnsAllSpecs_WhenNoFiltersProvided()
    {
        // Arrange
        var summaries = new List<DecisionSpecSummary>
        {
            new() { SpecId = "spec1", Name = "Test Spec 1", Status = "Draft", Owner = "user1", TraitCount = 3 },
            new() { SpecId = "spec2", Name = "Test Spec 2", Status = "Published", Owner = "user2", TraitCount = 5 }
        };
        _mockRepository.Setup(r => r.ListAsync(null, null, null, default))
            .ReturnsAsync(summaries);

        // Act
        var result = await _controller.List();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DecisionSpecListResponse>().Subject;
        response.Items.Should().HaveCount(2);
        response.Total.Should().Be(2);
    }

    [Fact]
    public async Task List_FiltersSpecs_WhenStatusProvided()
    {
        // Arrange
        var summaries = new List<DecisionSpecSummary>
        {
            new() { SpecId = "spec1", Name = "Test Spec 1", Status = "Published", Owner = "user1" }
        };
        _mockRepository.Setup(r => r.ListAsync("Published", null, null, default))
            .ReturnsAsync(summaries);

        // Act
        var result = await _controller.List(status: "Published");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DecisionSpecListResponse>().Subject;
        response.Items.Should().HaveCount(1);
        response.Items.First().Status.Should().Be("Published");
    }

    [Fact]
    public async Task List_SupportsPagination()
    {
        // Arrange
        var summaries = Enumerable.Range(1, 50)
            .Select(i => new DecisionSpecSummary 
            { 
                SpecId = $"spec{i}", 
                Name = $"Spec {i}", 
                Status = "Draft" 
            })
            .ToList();
        _mockRepository.Setup(r => r.ListAsync(null, null, null, default))
            .ReturnsAsync(summaries);

        // Act - Get page 2 with 20 items per page
        var result = await _controller.List(page: 2, pageSize: 20);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<DecisionSpecListResponse>().Subject;
        response.Items.Should().HaveCount(20);
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(20);
        response.Total.Should().Be(50);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_ReturnsCreated_WhenValidRequest()
    {
        // Arrange
        var request = CreateValidCreateRequest();
        var createdDoc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "sample-etag-123";

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<DecisionSpecDocument>(), default))
            .ReturnsAsync((createdDoc, etag));

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Should().NotBeNull();
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(_controller.Get));
        
        var dto = createdResult.Value.Should().BeOfType<DecisionSpecDocumentDto>().Subject;
        dto.SpecId.Should().Be("test-spec");
        
        _controller.Response.Headers.Should().ContainKey("ETag");
        _controller.Response.Headers["ETag"].ToString().Should().Be(etag);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenModelStateInvalid()
    {
        // Arrange
        var request = CreateValidCreateRequest();
        _controller.ModelState.AddModelError("SpecId", "Required");

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task Get_ReturnsSpec_WhenExists()
    {
        // Arrange
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "etag-123";
        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        // Act
        var result = await _controller.Get("test-spec");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DecisionSpecDocumentDto>().Subject;
        dto.SpecId.Should().Be("test-spec");
        
        _controller.Response.Headers.Should().ContainKey("ETag");
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAsync("nonexistent", null, default))
            .ReturnsAsync((ValueTuple<DecisionSpecDocument, string>?)null);

        // Act
        var result = await _controller.Get("nonexistent");

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_ReturnsOk_WhenValidETag()
    {
        // Arrange
        var request = CreateValidDocumentDto("test-spec", "1.0.0", "Draft");
        var updatedDoc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var newEtag = "new-etag-456";

        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), "old-etag", default))
            .ReturnsAsync((updatedDoc, newEtag));

        // Act
        var result = await _controller.Update("test-spec", request, "old-etag");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DecisionSpecDocumentDto>().Subject;
        dto.SpecId.Should().Be("test-spec");
        
        _controller.Response.Headers["ETag"].ToString().Should().Be(newEtag);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIfMatchHeaderMissing()
    {
        // Arrange
        var request = CreateValidDocumentDto("test-spec", "1.0.0", "Draft");

        // Act
        var result = await _controller.Update("test-spec", request, "");

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors.Should().ContainKey("If-Match");
    }

    [Fact]
    public async Task Update_ReturnsConflict_WhenETagMismatch()
    {
        // Arrange
        var request = CreateValidDocumentDto("test-spec", "1.0.0", "Draft");
        
        _mockRepository.Setup(r => r.UpdateAsync("test-spec", It.IsAny<DecisionSpecDocument>(), "wrong-etag", default))
            .ThrowsAsync(new InvalidOperationException("ETag mismatch"));

        // Act
        var result = await _controller.Update("test-spec", request, "wrong-etag");

        // Assert
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problemDetails = conflictResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Concurrency conflict");
        problemDetails.Status.Should().Be(409);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        var request = CreateValidDocumentDto("nonexistent", "1.0.0", "Draft");
        
        _mockRepository.Setup(r => r.UpdateAsync("nonexistent", It.IsAny<DecisionSpecDocument>(), "etag", default))
            .ReturnsAsync((ValueTuple<DecisionSpecDocument, string>?)null);

        // Act
        var result = await _controller.Update("nonexistent", request, "etag");

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        _mockRepository.Setup(r => r.DeleteAsync("test-spec", "1.0.0", "etag", default))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete("test-spec", "1.0.0", "etag");

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.DeleteAsync("nonexistent", "1.0.0", "etag", default))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete("nonexistent", "1.0.0", "etag");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenIfMatchMissing()
    {
        // Act
        var result = await _controller.Delete("test-spec", "1.0.0", "");

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors.Should().ContainKey("If-Match");
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenETagMismatch()
    {
        // Arrange
        _mockRepository.Setup(r => r.DeleteAsync("test-spec", "1.0.0", "wrong-etag", default))
            .ThrowsAsync(new InvalidOperationException("ETag mismatch"));

        // Act
        var result = await _controller.Delete("test-spec", "1.0.0", "wrong-etag");

        // Assert
        var conflictResult = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problemDetails = conflictResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Status.Should().Be(409);
    }

    #endregion

    #region Restore Tests

    [Fact]
    public async Task Restore_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var restoredDoc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var etag = "restored-etag";

        _mockRepository.Setup(r => r.RestoreAsync("test-spec", "1.0.0", default))
            .ReturnsAsync((restoredDoc, etag));

        // Act
        var result = await _controller.Restore("test-spec", "1.0.0");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DecisionSpecDocumentDto>().Subject;
        dto.SpecId.Should().Be("test-spec");
        
        _controller.Response.Headers["ETag"].ToString().Should().Be(etag);
    }

    [Fact]
    public async Task Restore_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.RestoreAsync("nonexistent", "1.0.0", default))
            .ReturnsAsync((ValueTuple<DecisionSpecDocument, string>?)null);

        // Act
        var result = await _controller.Restore("nonexistent", "1.0.0");

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Full Document Tests

    [Fact]
    public async Task GetFullDocument_ReturnsJson_WhenExists()
    {
        // Arrange
        var json = "{\"specId\":\"test-spec\",\"version\":\"1.0.0\"}";
        var doc = CreateSampleDocument("test-spec", "1.0.0", "Published");
        var etag = "etag-123";

        _mockRepository.Setup(r => r.GetFullDocumentJsonAsync("test-spec", null, default))
            .ReturnsAsync(json);
        _mockRepository.Setup(r => r.GetAsync("test-spec", null, default))
            .ReturnsAsync((doc, etag));

        // Act
        var result = await _controller.GetFullDocument("test-spec");

        // Assert
        var contentResult = result.Should().BeOfType<ContentResult>().Subject;
        contentResult.ContentType.Should().Be("application/json");
        contentResult.Content.Should().Be(json);
        
        _controller.Response.Headers.Should().ContainKey("ETag");
    }

    [Fact]
    public async Task GetFullDocument_ReturnsNotFound_WhenSpecDoesNotExist()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetFullDocumentJsonAsync("nonexistent", null, default))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _controller.GetFullDocument("nonexistent");

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Audit Tests

    [Fact]
    public async Task GetAuditHistory_ReturnsAuditLog()
    {
        // Arrange
        var entries = new List<AuditEntry>
        {
            new() { AuditId = "audit1", SpecId = "test-spec", Action = "Created", Actor = "user1", Summary = "Created spec" },
            new() { AuditId = "audit2", SpecId = "test-spec", Action = "Updated", Actor = "user2", Summary = "Updated metadata" }
        };

        _mockRepository.Setup(r => r.GetAuditHistoryAsync("test-spec", default))
            .ReturnsAsync(entries);

        // Act
        var result = await _controller.GetAuditHistory("test-spec");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<AuditLogResponse>().Subject;
        response.SpecId.Should().Be("test-spec");
        response.Events.Should().HaveCount(2);
        response.Events.First().Action.Should().Be("Created");
    }

    #endregion

    #region Patch Trait Tests

    [Fact]
    public async Task PatchTrait_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var request = new TraitPatchRequest
        {
            QuestionText = "Updated prompt",
            Comment = "Updated help text"
        };
        var patchedDoc = CreateSampleDocument("test-spec", "1.0.0", "Draft");
        var newEtag = "new-etag";

        _mockPatchService.Setup(s => s.PatchTraitAsync(
            "test-spec", "q1", "Updated prompt", null, null, null, "Updated help text", "old-etag", It.IsAny<string>(), default))
            .ReturnsAsync((patchedDoc, newEtag));

        // Act
        var result = await _controller.PatchTrait("test-spec", "q1", request, "old-etag");

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DecisionSpecDocumentDto>().Subject;
        dto.SpecId.Should().Be("test-spec");
        
        _controller.Response.Headers["ETag"].ToString().Should().Be(newEtag);
    }

    [Fact]
    public async Task PatchTrait_ReturnsBadRequest_WhenIfMatchMissing()
    {
        // Arrange
        var request = new TraitPatchRequest { QuestionText = "Updated prompt" };

        // Act
        var result = await _controller.PatchTrait("test-spec", "q1", request, "");

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors.Should().ContainKey("If-Match");
    }

    [Fact]
    public async Task PatchTrait_ReturnsNotFound_WhenTraitDoesNotExist()
    {
        // Arrange
        var request = new TraitPatchRequest { QuestionText = "Updated prompt" };

        _mockPatchService.Setup(s => s.PatchTraitAsync(
            "test-spec", "nonexistent", It.IsAny<string>(), It.IsAny<string>(), null, null, It.IsAny<string>(), "etag", It.IsAny<string>(), default))
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await _controller.PatchTrait("test-spec", "nonexistent", request, "etag");

        // Assert - Controller catches KeyNotFoundException and returns NotFoundObjectResult with validation details
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problemDetails = notFoundResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problemDetails.Errors.Should().ContainKey("traitKey");
    }

    #endregion

    #region Helper Methods

    private static DecisionSpecCreateRequest CreateValidCreateRequest()
    {
        return new DecisionSpecCreateRequest
        {
            SpecId = "test-spec",
            Version = "1.0.0",
            Status = "Draft",
            Metadata = new DecisionSpecMetadataDto
            {
                Name = "Test Spec",
                Description = "A test specification",
                Tags = new List<string> { "test" }
            },
            Traits = new List<TraitDto>
            {
                new()
                {
                    Key = "q1",
                    AnswerType = "SingleSelect",
                    QuestionText = "Test question?",
                    Required = true,
                    Options = new List<OptionDto>
                    {
                        new() { Key = "o1", Label = "Option 1", Value = "opt1" }
                    }
                }
            },
            Outcomes = new List<OutcomeDto>
            {
                new()
                {
                    OutcomeId = "outcome1",
                    SelectionRules = new List<string> { "q1:opt1" },
                    DisplayCards = new List<object>()
                }
            }
        };
    }

    private static DecisionSpecDocumentDto CreateValidDocumentDto(string specId, string version, string status)
    {
        return new DecisionSpecDocumentDto
        {
            SpecId = specId,
            Version = version,
            Status = status,
            Metadata = new DecisionSpecMetadataDto
            {
                Name = "Test Spec",
                Description = "A test specification",
                Tags = new List<string> { "test" }
            },
            Traits = new List<TraitDto>
            {
                new()
                {
                    Key = "q1",
                    AnswerType = "SingleSelect",
                    QuestionText = "Test question?",
                    Required = true,
                    Options = new List<OptionDto>
                    {
                        new() { Key = "o1", Label = "Option 1", Value = "opt1" }
                    }
                }
            },
            Outcomes = new List<OutcomeDto>
            {
                new()
                {
                    OutcomeId = "outcome1",
                    SelectionRules = new List<string> { "q1:opt1" },
                    DisplayCards = new List<object>()
                }
            }
        };
    }

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
                Tags = new List<string> { "test" }
            },
            Traits = new List<TraitDefinition>
            {
                new()
                {
                    Key = "q1",
                    AnswerType = "SingleSelect",
                    QuestionText = "Test question?",
                    Required = true,
                    Options = new List<string> { "Option 1" }
                }
            },
            Outcomes = new List<OutcomeDefinition>
            {
                new()
                {
                    OutcomeId = "outcome1",
                    SelectionRules = new List<string> { "q1:opt1" },
                    DisplayCards = new List<DisplayCard>()
                }
            }
        };
    }

    #endregion
}
