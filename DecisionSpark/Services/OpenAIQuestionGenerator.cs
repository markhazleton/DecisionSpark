using DecisionSpark.Models.Spec;
using System.Text.Json;

namespace DecisionSpark.Services;

/// <summary>
/// OpenAI-powered question generator that creates contextual, rephrased questions
/// </summary>
public class OpenAIQuestionGenerator : IQuestionGenerator
{
    private readonly ILogger<OpenAIQuestionGenerator> _logger;
    private readonly IOpenAIService _openAIService;

    public OpenAIQuestionGenerator(
        ILogger<OpenAIQuestionGenerator> logger,
        IOpenAIService openAIService)
    {
        _logger = logger;
        _openAIService = openAIService;
    }

    public async Task<string> GenerateQuestionAsync(DecisionSpec spec, TraitDefinition trait, int retryAttempt = 0)
    {
        _logger.LogDebug("Generating question for trait {TraitKey}, retry attempt {Attempt}", 
            trait.Key, retryAttempt);

        // If OpenAI is not available, fall back to original question text
        if (!_openAIService.IsAvailable())
        {
            _logger.LogDebug("OpenAI not available, using base question text");
            return GetFallbackQuestion(trait, retryAttempt);
        }

        try
        {
            var systemPrompt = BuildSystemPrompt(spec, trait, retryAttempt);
            var userPrompt = BuildUserPrompt(trait, retryAttempt);

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 150,
                Temperature = 0.7f
            };

            _logger.LogDebug("Requesting OpenAI to generate question for {TraitKey}", trait.Key);

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var generatedQuestion = response.Content.Trim();
                _logger.LogInformation("Generated question for {TraitKey}: {Question}", 
                    trait.Key, generatedQuestion);
                return generatedQuestion;
            }

            if (response.UsedFallback)
            {
                _logger.LogWarning("OpenAI failed, using fallback question for {TraitKey}", trait.Key);
                return GetFallbackQuestion(trait, retryAttempt);
            }

            _logger.LogError("OpenAI returned empty content for {TraitKey}: {Error}", 
                trait.Key, response.ErrorMessage);
            return GetFallbackQuestion(trait, retryAttempt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating question with OpenAI for trait {TraitKey}", trait.Key);
            return GetFallbackQuestion(trait, retryAttempt);
        }
    }

    private string BuildSystemPrompt(DecisionSpec spec, TraitDefinition trait, int retryAttempt)
    {
        var systemPrompt = $@"You are a helpful assistant generating clear, concise questions for a decision-making system.

Safety Guidelines: {spec.SafetyPreamble}

Your task:
- Generate a natural, conversational question to collect information
- Keep it brief and easy to understand
- Make it sound friendly but professional
- The question should collect: {trait.AnswerType}
{(trait.Bounds != null ? $"- Valid range: {trait.Bounds.Min} to {trait.Bounds.Max}" : "")}
{(retryAttempt > 0 ? "- This is a retry after invalid input, so rephrase to be clearer about what format is needed" : "")}

Return ONLY the question text, nothing else.";

        return systemPrompt;
    }

    private string BuildUserPrompt(TraitDefinition trait, int retryAttempt)
    {
        var context = new
        {
            trait_key = trait.Key,
            base_question = trait.QuestionText,
            answer_type = trait.AnswerType,
            parse_hint = trait.ParseHint,
            retry_attempt = retryAttempt,
            options = trait.Options
        };

        if (retryAttempt > 0)
        {
            return $@"The user gave invalid input for this question. Generate a rephrased version that's clearer about the expected format.

Base question: {trait.QuestionText}
Expected format: {trait.ParseHint}
Retry attempt: {retryAttempt}

Generate a rephrased question that helps the user understand what format is needed.";
        }

        return $@"Generate a natural, conversational version of this question:

Base question: {trait.QuestionText}
Context: {JsonSerializer.Serialize(context)}

Make it sound friendly and easy to understand while collecting the same information.";
    }

    private string GetFallbackQuestion(TraitDefinition trait, int retryAttempt)
    {
        var question = trait.QuestionText;

        if (retryAttempt > 0)
        {
            var hints = new List<string>();

            if (trait.AnswerType == "integer")
            {
                hints.Add("Please provide a single number");
                if (trait.Bounds != null)
                {
                    hints.Add($"between {trait.Bounds.Min} and {trait.Bounds.Max}");
                }
            }
            else if (trait.AnswerType == "integer_list")
            {
                hints.Add("Please provide a comma-separated list of numbers");
                if (trait.Bounds != null)
                {
                    hints.Add($"each between {trait.Bounds.Min} and {trait.Bounds.Max}");
                }
            }
            else if (trait.AnswerType == "enum" && trait.Options != null)
            {
                hints.Add($"Please choose from: {string.Join(", ", trait.Options)}");
            }

            var hintText = hints.Any() ? $" ({string.Join(", ", hints)})" : "";
            return $"Let me try again. {question}{hintText}";
        }

        return question;
    }
}
