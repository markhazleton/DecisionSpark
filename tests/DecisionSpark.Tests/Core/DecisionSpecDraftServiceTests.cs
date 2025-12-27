using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DecisionSpark.Tests.Core;

/// <summary>
/// Tests for DecisionSpecDraftService (LLM-assisted spec generation).
/// </summary>
public class DecisionSpecDraftServiceTests : IDisposable
{
    private readonly Mock<IOpenAIService> _mockOpenAIService;
    private readonly Mock<IDecisionSpecRepository> _mockRepository;
    private readonly Mock<IValidator<DecisionSpecDocument>> _mockValidator;
    private readonly Mock<ILogger<DecisionSpecDraftService>> _mockLogger;
    private readonly string _testDraftsPath;
    private readonly DecisionSpecDraftService _service;

    public DecisionSpecDraftServiceTests()
    {
        _mockOpenAIService = new Mock<IOpenAIService>();
        _mockRepository = new Mock<IDecisionSpecRepository>();
        _mockValidator = new Mock<IValidator<DecisionSpecDocument>>();
        _mockLogger = new Mock<ILogger<DecisionSpecDraftService>>();
        
        _testDraftsPath = Path.Combine(Path.GetTempPath(), $"test-drafts-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDraftsPath);

        _service = new DecisionSpecDraftService(
            _mockOpenAIService.Object,
            _mockRepository.Object,
            _mockValidator.Object,
            _mockLogger.Object,
            _testDraftsPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDraftsPath))
        {
            Directory.Delete(_testDraftsPath, true);
        }
    }

    [Fact]
    public async Task GenerateDraftAsync_WithValidInstruction_ReturnsUnverifiedDraft()
    {
        // Arrange
        var instruction = "Create a spec for selecting cloud providers";
        var llmResponse = @"{
            ""spec_id"": ""cloud_provider"",
            ""version"": ""V1.0.0.0"",
            ""metadata"": {
                ""name"": ""Cloud Provider Selection"",
                ""description"": ""Help users choose the right cloud provider""
            },
            ""traits"": [
                {
                    ""key"": ""workload_type"",
                    ""question_text"": ""What type of workload?"",
                    ""answer_type"": ""select"",
                    ""required"": true,
                    ""options"": [""Web Apps"", ""Data Analytics"", ""ML/AI""]
                }
            ],
            ""outcomes"": [
                {
                    ""outcome_id"": ""aws"",
                    ""selection_rules"": [""workload_type == 'Web Apps'""],
                    ""display_cards"": [
                        {
                            ""title"": ""AWS"",
                            ""subtitle"": ""Amazon Web Services"",
                            ""body_text"": [""Best for web applications""]
                        }
                    ]
                }
            ]
        }";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Act
        var (draft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Assert
        Assert.NotNull(draft);
        Assert.NotNull(draftId);
        Assert.True(draft.Metadata.Unverified);
        Assert.Equal("Draft", draft.Status);
        Assert.Equal("LLM", draft.Metadata.CreatedBy);
        Assert.Single(draft.Traits);
        Assert.Single(draft.Outcomes);
    }

    [Fact]
    public async Task GenerateDraftAsync_EmptyInstruction_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.GenerateDraftAsync(""));
    }

    [Fact]
    public async Task GenerateDraftAsync_InstructionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longInstruction = new string('a', 2001);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.GenerateDraftAsync(longInstruction));
    }

    [Fact]
    public async Task GenerateDraftAsync_WithValidationErrors_LogsWarningsButDoesNotThrow()
    {
        // Arrange
        var instruction = "Create a spec";
        var llmResponse = @"{""spec_id"": ""test"", ""traits"": [], ""outcomes"": []}";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        var validationErrors = new List<ValidationFailure>
        {
            new ValidationFailure("Traits", "At least one trait is required")
        };
        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationErrors));

        // Act
        var (draft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Assert
        Assert.NotNull(draft);
        Assert.True(draft.Metadata.Unverified);
        
        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("validation errors")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateDraftAsync_LlmReturnsInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var instruction = "Create a spec";
        var invalidJson = "This is not JSON";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = invalidJson
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.GenerateDraftAsync(instruction));
        
        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public async Task GenerateDraftAsync_LlmReturnsEmpty_ThrowsInvalidOperationException()
    {
        // Arrange
        var instruction = "Create a spec";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = false,
                ErrorMessage = "Empty response"
            });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.GenerateDraftAsync(instruction));
        
        Assert.Contains("empty response", ex.Message);
    }

    [Fact]
    public async Task GenerateDraftAsync_CachesDraftToFileSystem()
    {
        // Arrange
        var instruction = "Create a spec";
        var llmResponse = @"{""spec_id"": ""test"", ""traits"": [], ""outcomes"": []}";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Act
        var (draft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Assert
        var draftFile = Path.Combine(_testDraftsPath, $"{draftId}.json");
        Assert.True(File.Exists(draftFile));
    }

    [Fact]
    public async Task GetDraftAsync_WithExistingDraft_ReturnsDraft()
    {
        // Arrange
        var instruction = "Create a spec";
        var llmResponse = @"{""spec_id"": ""test"", ""traits"": [], ""outcomes"": []}";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var (originalDraft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Act
        var retrievedDraft = await _service.GetDraftAsync(draftId);

        // Assert
        Assert.NotNull(retrievedDraft);
        Assert.Equal(originalDraft.SpecId, retrievedDraft.SpecId);
    }

    [Fact]
    public async Task GetDraftAsync_WithNonExistentDraft_ReturnsNull()
    {
        // Act
        var draft = await _service.GetDraftAsync("non-existent-id");

        // Assert
        Assert.Null(draft);
    }

    [Fact]
    public async Task ClearDraftAsync_RemovesDraftFromCache()
    {
        // Arrange
        var instruction = "Create a spec";
        var llmResponse = @"{""spec_id"": ""test"", ""traits"": [], ""outcomes"": []}";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var (draft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Act
        await _service.ClearDraftAsync(draftId);

        // Assert
        var draftFile = Path.Combine(_testDraftsPath, $"{draftId}.json");
        Assert.False(File.Exists(draftFile));
    }

    [Fact]
    public async Task GenerateDraftAsync_HandlesMarkdownCodeBlocks()
    {
        // Arrange
        var instruction = "Create a spec";
        var llmResponse = @"```json
{
    ""spec_id"": ""test"",
    ""traits"": [],
    ""outcomes"": []
}
```";

        _mockOpenAIService
            .Setup(x => x.GetCompletionAsync(It.IsAny<OpenAICompletionRequest>()))
            .ReturnsAsync(new OpenAICompletionResponse
            {
                Success = true,
                Content = llmResponse
            });

        _mockValidator
            .Setup(x => x.ValidateAsync(It.IsAny<DecisionSpecDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Act
        var (draft, draftId) = await _service.GenerateDraftAsync(instruction);

        // Assert
        Assert.NotNull(draft);
        Assert.Equal("test", draft.SpecId);
    }
}
