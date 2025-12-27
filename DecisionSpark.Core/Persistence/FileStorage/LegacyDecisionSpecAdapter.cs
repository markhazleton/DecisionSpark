using System.Text.Json;
using DecisionSpark.Core.Models.Spec;
using Microsoft.Extensions.Logging;

namespace DecisionSpark.Core.Persistence.FileStorage;

/// <summary>
/// Adapter to convert legacy DecisionSpec format (trait-based) to new DecisionSpecDocument format (question-based).
/// Provides backward compatibility for existing spec files.
/// </summary>
public class LegacyDecisionSpecAdapter
{
    private readonly ILogger<LegacyDecisionSpecAdapter> _logger;

    public LegacyDecisionSpecAdapter(ILogger<LegacyDecisionSpecAdapter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to convert a legacy DecisionSpec JSON to DecisionSpecDocument.
    /// </summary>
    public DecisionSpecDocument? ConvertLegacySpec(string jsonContent, string fileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Check if this is a legacy format by looking for spec_id (underscore) instead of specId (camelCase)
            if (!root.TryGetProperty("spec_id", out var specIdProp))
            {
                return null; // Not a legacy format
            }

            var specId = specIdProp.GetString() ?? string.Empty;
            var version = root.TryGetProperty("version", out var versionProp) ? versionProp.GetString() ?? "1.0.0" : "1.0.0";

            // Extract status from filename (e.g., "TECH_STACK_ADVISOR_V1.0.0.0.active.json")
            var status = fileName.Contains(".active.", StringComparison.OrdinalIgnoreCase) ? "Published" : "Draft";

            var document = new DecisionSpecDocument
            {
                SpecId = specId,
                Version = version,
                Status = status,
                Metadata = ExtractMetadata(root, specId),
                Questions = ConvertTraitsToQuestions(root),
                Outcomes = ConvertOutcomes(root)
            };

            _logger.LogInformation("Converted legacy spec {SpecId} to new format", specId);
            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert legacy spec from file {FileName}", fileName);
            return null;
        }
    }

    private static DecisionSpecMetadata ExtractMetadata(JsonElement root, string specId)
    {
        var metadata = new DecisionSpecMetadata
        {
            Name = specId.Replace("_", " ").Replace("V1", "v1"),
            Description = $"Imported from legacy format",
            Owner = "System",
            Tags = new List<string> { "legacy", "imported" },
            Unverified = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "LegacyAdapter",
            UpdatedBy = "LegacyAdapter"
        };

        return metadata;
    }

    private static List<Question> ConvertTraitsToQuestions(JsonElement root)
    {
        var questions = new List<Question>();

        if (!root.TryGetProperty("traits", out var traitsArray))
        {
            return questions;
        }

        var questionIndex = 1;
        foreach (var trait in traitsArray.EnumerateArray())
        {
            if (!trait.TryGetProperty("key", out var keyProp))
                continue;

            var key = keyProp.GetString() ?? string.Empty;
            var questionText = trait.TryGetProperty("question_text", out var qtProp) ? qtProp.GetString() ?? "" : key;
            var answerType = trait.TryGetProperty("answer_type", out var atProp) ? atProp.GetString() ?? "text" : "text";
            var required = trait.TryGetProperty("required", out var reqProp) && reqProp.GetBoolean();

            var question = new Question
            {
                QuestionId = $"q{questionIndex++}",
                Type = MapAnswerTypeToQuestionType(answerType),
                Prompt = questionText,
                HelpText = $"Legacy trait: {key}",
                Required = required,
                Options = new List<Option>()
            };

            // Extract options if available
            if (trait.TryGetProperty("options", out var optionsArray))
            {
                var optionIndex = 1;
                foreach (var option in optionsArray.EnumerateArray())
                {
                    var optionValue = option.GetString() ?? string.Empty;
                    question.Options.Add(new Option
                    {
                        OptionId = $"opt{optionIndex++}",
                        Label = optionValue,
                        Value = optionValue
                    });
                }
            }

            questions.Add(question);
        }

        return questions;
    }

    private static List<Outcome> ConvertOutcomes(JsonElement root)
    {
        var outcomes = new List<Outcome>();

        if (!root.TryGetProperty("outcomes", out var outcomesArray))
        {
            return outcomes;
        }

        foreach (var outcome in outcomesArray.EnumerateArray())
        {
            if (!outcome.TryGetProperty("outcome_id", out var idProp))
                continue;

            var outcomeId = idProp.GetString() ?? string.Empty;

            var newOutcome = new Outcome
            {
                OutcomeId = outcomeId,
                SelectionRules = new List<string> { "true" }, // Legacy specs don't map directly
                DisplayCards = new List<OutcomeDisplayCard>()
            };

            // Extract display cards if available
            if (outcome.TryGetProperty("display_cards", out var cardsArray))
            {
                foreach (var card in cardsArray.EnumerateArray())
                {
                    var title = card.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";
                    var subtitle = card.TryGetProperty("subtitle", out var subtitleProp) ? subtitleProp.GetString() ?? "" : "";

                    newOutcome.DisplayCards.Add(new OutcomeDisplayCard
                    {
                        Title = title,
                        Description = subtitle
                    });
                }
            }

            outcomes.Add(newOutcome);
        }

        return outcomes;
    }

    private static string MapAnswerTypeToQuestionType(string answerType)
    {
        return answerType.ToLowerInvariant() switch
        {
            "single_select" => "SingleSelect",
            "multi_select" => "MultiSelect",
            "text" => "Text",
            "number" => "Number",
            "date" => "Date",
            _ => "Text"
        };
    }

    /// <summary>
    /// Checks if a JSON string represents a legacy format spec.
    /// </summary>
    public static bool IsLegacyFormat(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;
            
            // Legacy format has spec_id (underscore), new format has specId (camelCase)
            return root.TryGetProperty("spec_id", out _);
        }
        catch
        {
            return false;
        }
    }
}
