using DecisionSpark.Core.Models.Api;
using DecisionSpark.Core.Models.Spec;

namespace DecisionSpark.Core.Services;

public interface IQuestionGenerator
{
    Task<string> GenerateQuestionAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0);
    Task<QuestionGenerationResult> GenerateQuestionWithOptionsAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0);
}

public class QuestionGenerationResult
{
    public string QuestionText { get; set; } = string.Empty;
    public List<QuestionOptionDto> Options { get; set; } = new();
    public QuestionMetadataDto? Metadata { get; set; }
}

public class StubQuestionGenerator : IQuestionGenerator
{
    private readonly ILogger<StubQuestionGenerator> _logger;

    public StubQuestionGenerator(ILogger<StubQuestionGenerator> logger)
    {
 _logger = logger;
  }

    public Task<string> GenerateQuestionAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0)
    {
  _logger.LogDebug("Generating question for trait {TraitKey}, retry attempt {Attempt}", trait.Key, retryAttempt);

        // For now, return the base question text
      // Later this will call OpenAI for phrasing
    var question = trait.QuestionText;

        if (retryAttempt > 0)
        {
       question = $"Let me try again. {question}";
        }

        return Task.FromResult(question);
    }

    public Task<QuestionGenerationResult> GenerateQuestionWithOptionsAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0)
    {
        var result = new QuestionGenerationResult
        {
            QuestionText = trait.QuestionText,
            Options = new List<QuestionOptionDto>(),
            Metadata = new QuestionMetadataDto()
        };

        if (retryAttempt > 0)
        {
            result.QuestionText = $"Let me try again. {trait.QuestionText}";
        }

        return Task.FromResult(result);
    }
}
