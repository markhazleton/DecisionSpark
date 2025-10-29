using DecisionSpark.Models.Spec;

namespace DecisionSpark.Services;

public interface IQuestionGenerator
{
    Task<string> GenerateQuestionAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0);
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
}
