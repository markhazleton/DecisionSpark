using DecisionSpark.Models.Runtime;
using DecisionSpark.Models.Spec;

namespace DecisionSpark.Services;

/// <summary>
/// Determines which question presentation type (text, single-select, multi-select)
/// should be used based on LLM output, trait configuration, and validation history.
/// Implements the renderer arbitration logic per FR-007, FR-033, FR-043, FR-044.
/// </summary>
public interface IQuestionPresentationDecider
{
    /// <summary>
    /// Decides the presentation type for the current question.
    /// </summary>
    /// <param name="trait">The trait definition</param>
    /// <param name="session">The current session with validation history</param>
    /// <param name="llmSuggestedType">Optional type suggested by LLM (text/single-select/multi-select)</param>
    /// <returns>The determined question type</returns>
    string DecideQuestionType(TraitDefinition trait, DecisionSession session, string? llmSuggestedType = null);
}

public class QuestionPresentationDecider : IQuestionPresentationDecider
{
    private readonly ILogger<QuestionPresentationDecider> _logger;

    public QuestionPresentationDecider(ILogger<QuestionPresentationDecider> logger)
    {
        _logger = logger;
    }

    public string DecideQuestionType(TraitDefinition trait, DecisionSession session, string? llmSuggestedType = null)
    {
        // Get validation failures for this trait
        var validationFailures = session.ValidationHistory
            ?.Where(v => v.TraitKey == trait.Key)
            .OrderByDescending(v => v.TimestampUtc)
            .ToList() ?? new List<ValidationHistoryEntry>();

        var attemptCount = validationFailures.Count;

        _logger.LogDebug(
            "[QuestionPresentationDecider] Deciding type for trait '{TraitKey}'. Attempts: {Attempts}, LLM suggestion: '{LlmType}'",
            trait.Key, attemptCount, llmSuggestedType ?? "none");

        // FR-044: After 3rd attempt, force static fallback (text input)
        if (attemptCount >= 3)
        {
            _logger.LogInformation(
                "[QuestionPresentationDecider] Forcing text input fallback after {Attempts} failed attempts for trait '{TraitKey}'",
                attemptCount, trait.Key);
            return "text";
        }

        // FR-007: On retry, consider switching input type
        if (attemptCount > 0)
        {
            var lastFailure = validationFailures.First();
            _logger.LogDebug(
                "[QuestionPresentationDecider] Previous failure detected. Last input type: '{LastType}', Reason: '{Reason}'",
                lastFailure.InputTypeUsed, lastFailure.ErrorReason);

            // If structured input failed, try text
            if (lastFailure.InputTypeUsed == "single-select" || lastFailure.InputTypeUsed == "multi-select")
            {
                _logger.LogInformation(
                    "[QuestionPresentationDecider] Switching from structured to text input after failure for trait '{TraitKey}'",
                    trait.Key);
                return "text";
            }

            // If text failed and we have options available, try structured
            if (lastFailure.InputTypeUsed == "text" && trait.Options != null && trait.Options.Count > 0)
            {
                var structuredType = DetermineStructuredType(trait);
                _logger.LogInformation(
                    "[QuestionPresentationDecider] Switching from text to {Type} after failure for trait '{TraitKey}'",
                    structuredType, trait.Key);
                return structuredType;
            }
        }

        // First attempt: use LLM suggestion if available and valid
        if (!string.IsNullOrWhiteSpace(llmSuggestedType))
        {
            var validTypes = new[] { "text", "single-select", "multi-select" };
            if (validTypes.Contains(llmSuggestedType))
            {
                _logger.LogDebug(
                    "[QuestionPresentationDecider] Using LLM suggestion '{Type}' for trait '{TraitKey}'",
                    llmSuggestedType, trait.Key);
                return llmSuggestedType;
            }

            _logger.LogWarning(
                "[QuestionPresentationDecider] Invalid LLM suggestion '{Type}' for trait '{TraitKey}', falling back to trait config",
                llmSuggestedType, trait.Key);
        }

        // Fallback: determine from trait configuration
        var determinedType = DetermineFromTraitConfig(trait);
        _logger.LogDebug(
            "[QuestionPresentationDecider] Using trait config-based type '{Type}' for trait '{TraitKey}'",
            determinedType, trait.Key);

        return determinedType;
    }

    private string DetermineStructuredType(TraitDefinition trait)
    {
        // Multi-select if explicitly configured
        if (trait.AllowMultiple == true)
        {
            return "multi-select";
        }

        // Single-select if we have options but no multi-select
        if (trait.Options != null && trait.Options.Count > 0)
        {
            return "single-select";
        }

        return "text";
    }

    private string DetermineFromTraitConfig(TraitDefinition trait)
    {
        // Check if trait has enumerated options
        if (trait.AnswerType == "enum" && trait.Options != null && trait.Options.Count > 0)
        {
            // Check for multi-select capability
            if (trait.AllowMultiple == true)
            {
                return "multi-select";
            }

            return "single-select";
        }

        // For integer_list types, use multi-select if options available
        if (trait.AnswerType == "integer_list" && trait.Options != null && trait.Options.Count > 0)
        {
            return "multi-select";
        }

        // Default to text for all other cases
        return "text";
    }
}
