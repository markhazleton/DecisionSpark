using DecisionSpark.Core.Models.Runtime;
using DecisionSpark.Core.Models.Spec;
using System.Text;

namespace DecisionSpark.Core.Services;

public interface IRoutingEvaluator
{
    Task<EvaluationResult> EvaluateAsync(DecisionSpec spec, Dictionary<string, object> knownTraits);
}

public class RoutingEvaluator : IRoutingEvaluator
{
    private readonly ILogger<RoutingEvaluator> _logger;
    private readonly IOpenAIService _openAIService;

    public RoutingEvaluator(
        ILogger<RoutingEvaluator> logger,
        IOpenAIService openAIService)
    {
        _logger = logger;
        _openAIService = openAIService;
    }

    public async Task<EvaluationResult> EvaluateAsync(DecisionSpec spec, Dictionary<string, object> knownTraits)
    {
        var result = await EvaluateInternalAsync(spec, knownTraits);

        if (result.IsComplete && result.Outcome != null && string.IsNullOrEmpty(result.FinalSummary))
        {
            if (_openAIService.IsAvailable())
            {
                try 
                {
                    result.FinalSummary = await GenerateOutcomeSummaryAsync(spec, result.Outcome, knownTraits);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate final summary");
                }
            }
        }

        return result;
    }

    private async Task<EvaluationResult> EvaluateInternalAsync(DecisionSpec spec, Dictionary<string, object> knownTraits)
    {
        _logger.LogDebug("Evaluating with {TraitCount} known traits", knownTraits.Count);

        // Compute derived traits
        var allTraits = ComputeDerivedTraits(spec, knownTraits);

        // Check immediate select rules first
        foreach (var immediateRule in spec.ImmediateSelectIf)
        {
            if (EvaluateRule(immediateRule.Rule, allTraits))
            {
                var outcome = spec.Outcomes.FirstOrDefault(o => o.OutcomeId == immediateRule.OutcomeId);
                if (outcome != null)
                {
                    _logger.LogInformation("Immediate rule matched: {OutcomeId}", outcome.OutcomeId);
                    return new EvaluationResult
                    {
                        IsComplete = true,
                        Outcome = outcome,
                        ResolutionMode = "IMMEDIATE"
                    };
                }
            }
        }

        // Evaluate each outcome's selection rules
        var satisfiedOutcomes = new List<OutcomeDefinition>();
        foreach (var outcome in spec.Outcomes)
        {
            var allRulesSatisfied = outcome.SelectionRules.All(rule => EvaluateRule(rule, allTraits));
            if (allRulesSatisfied)
            {
                satisfiedOutcomes.Add(outcome);
            }
        }

        // If exactly one outcome satisfied, return it
        if (satisfiedOutcomes.Count == 1)
        {
            _logger.LogInformation("Single outcome matched: {OutcomeId}", satisfiedOutcomes[0].OutcomeId);
            return new EvaluationResult
            {
                IsComplete = true,
                Outcome = satisfiedOutcomes[0],
                ResolutionMode = "SINGLE_MATCH"
            };
        }

        // If multiple outcomes satisfied, handle tie
        if (satisfiedOutcomes.Count > 1)
        {
            _logger.LogInformation("Tie detected: {Count} outcomes", satisfiedOutcomes.Count);
            return await HandleTieAsync(spec, satisfiedOutcomes, knownTraits);
        }

        // No outcome yet, determine next trait to ask
        var nextTrait = DetermineNextTrait(spec, knownTraits);
        if (nextTrait != null)
        {
            _logger.LogDebug("Next trait to collect: {TraitKey}", nextTrait.Key);
            return new EvaluationResult
            {
                IsComplete = false,
                NextTraitKey = nextTrait.Key,
                NextTraitDefinition = nextTrait
            };
        }

        // No traits left but no outcome - should not happen with valid spec
        _logger.LogWarning("No outcome and no next trait - defaulting to first outcome");
        return new EvaluationResult
        {
            IsComplete = true,
            Outcome = spec.Outcomes.First(),
            ResolutionMode = "FALLBACK"
        };
    }

    private async Task<EvaluationResult> HandleTieAsync(
        DecisionSpec spec,
        List<OutcomeDefinition> tiedOutcomes,
        Dictionary<string, object> knownTraits)
    {
        _logger.LogInformation("Handling tie between {Count} outcomes: {Outcomes}",
            tiedOutcomes.Count,
            string.Join(", ", tiedOutcomes.Select(o => o.OutcomeId)));

        // Check if tie strategy is configured
        if (spec.TieStrategy == null || spec.TieStrategy.Mode != "LLM_CLARIFIER")
        {
            _logger.LogWarning("No LLM clarifier configured, returning first tied outcome");
            return new EvaluationResult
            {
                IsComplete = true,
                Outcome = tiedOutcomes[0],
                ResolutionMode = "TIE_FALLBACK"
            };
        }

        // Check if we have an answer to a previous LLM clarifier
        var llmAnswerKey = knownTraits.Keys.FirstOrDefault(k => k.StartsWith("llm_clarifier_"));
        if (llmAnswerKey != null && knownTraits.TryGetValue(llmAnswerKey, out var answerObj))
        {
            var answerText = answerObj?.ToString();
            if (!string.IsNullOrWhiteSpace(answerText))
            {
                _logger.LogInformation("Found answer to LLM clarifier: {Answer}", answerText);
                
                // Use LLM to pick the winner based on the answer
                var (winner, summary) = await PickWinnerWithLLMAsync(spec, tiedOutcomes, answerText, knownTraits);
                if (winner != null)
                {
                    return new EvaluationResult
                    {
                        IsComplete = true,
                        Outcome = winner,
                        ResolutionMode = "LLM_RESOLVED",
                        FinalSummary = summary
                    };
                }
            }
        }

        // Check if we should ask a pseudo-trait question
        var pseudoTrait = FindNextPseudoTrait(spec, knownTraits);
        if (pseudoTrait != null)
        {
            _logger.LogInformation("Asking pseudo-trait question to resolve tie: {TraitKey}", pseudoTrait.Key);
            return new EvaluationResult
            {
                IsComplete = false,
                RequiresClarifier = true,
                TiedOutcomes = tiedOutcomes,
                NextTraitKey = pseudoTrait.Key,
                NextTraitDefinition = pseudoTrait,
                ResolutionMode = "PSEUDO_TRAIT_CLARIFIER"
            };
        }

        // Try LLM-generated clarifying question
        if (_openAIService.IsAvailable())
        {
            var clarifierResult = await GenerateClarifyingQuestionAsync(spec, tiedOutcomes, knownTraits);
            if (clarifierResult != null && !string.IsNullOrEmpty(clarifierResult.QuestionText))
            {
                _logger.LogInformation("Generated LLM clarifying question for tie");
                // Create a dynamic pseudo-trait for this clarifier
                var dynamicTrait = new TraitDefinition
                {
                    Key = $"llm_clarifier_{Guid.NewGuid():N}",
                    QuestionText = clarifierResult.QuestionText,
                    AnswerType = clarifierResult.QuestionType,
                    ParseHint = "User's preference to resolve tie",
                    Required = false,
                    IsPseudoTrait = true,
                    Options = clarifierResult.Options,
                    AllowMultiple = clarifierResult.QuestionType == "enum_list"
                };

                return new EvaluationResult
                {
                    IsComplete = false,
                    RequiresClarifier = true,
                    TiedOutcomes = tiedOutcomes,
                    NextTraitKey = dynamicTrait.Key,
                    NextTraitDefinition = dynamicTrait,
                    ResolutionMode = "LLM_CLARIFIER"
                };
            }
        }

        // Fallback: return first outcome
        _logger.LogWarning("Could not resolve tie with LLM, using first outcome");
        return new EvaluationResult
        {
            IsComplete = true,
            Outcome = tiedOutcomes[0],
            ResolutionMode = "TIE_FALLBACK"
        };
    }

    private TraitDefinition? FindNextPseudoTrait(DecisionSpec spec, Dictionary<string, object> knownTraits)
    {
        if (spec.TieStrategy?.PseudoTraits == null)
            return null;

        foreach (var pseudoTrait in spec.TieStrategy.PseudoTraits)
        {
            if (!knownTraits.ContainsKey(pseudoTrait.Key))
            {
                return pseudoTrait;
            }
        }

        return null;
    }

    private async Task<(OutcomeDefinition? Outcome, string? Summary)> PickWinnerWithLLMAsync(
        DecisionSpec spec,
        List<OutcomeDefinition> tiedOutcomes,
        string userAnswer,
        Dictionary<string, object> knownTraits)
    {
        try
        {
            var outcomeSummaries = BuildOutcomeSummaries(tiedOutcomes);
            var knownTraitsSummary = BuildKnownTraitsSummary(knownTraits);
            
            var systemPrompt = $@"You are a decision helper.
Safety Guidelines: {spec.SafetyPreamble}

Context:
The user has provided the following information:
{knownTraitsSummary}

Your task: 
1. Select the best outcome from the candidates based on the user's preference and known traits.
2. Provide a short summary explaining why this outcome was chosen, referencing the user's specific traits (e.g., group size, ages, preferences).

Format your response exactly as:
WINNER: [OutcomeId]
SUMMARY: [Your explanation]

If none match well, return 'WINNER: NONE'.";

            var userPrompt = $@"Candidate Outcomes:
{outcomeSummaries}

User Preference to Clarifying Question: ""{userAnswer}""

Which outcome ID is the best match and why?";

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 300,
                Temperature = 0.3f
            };

            _logger.LogDebug("Requesting OpenAI to pick winner from tie");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var content = response.Content.Trim();
                string winnerId = "NONE";
                string summary = "";

                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("WINNER:", StringComparison.OrdinalIgnoreCase))
                    {
                        winnerId = line.Substring(7).Trim();
                    }
                    else if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
                    {
                        summary = line.Substring(8).Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(summary))
                    {
                        // Append multi-line summary
                        summary += " " + line.Trim();
                    }
                }

                var winner = tiedOutcomes.FirstOrDefault(o => o.OutcomeId.Equals(winnerId, StringComparison.OrdinalIgnoreCase));
                
                if (winner != null)
                {
                    _logger.LogInformation("LLM picked winner: {OutcomeId}", winner.OutcomeId);
                    return (winner, summary);
                }
                
                _logger.LogWarning("LLM returned invalid outcome ID: {Id}", winnerId);
            }
            
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error picking winner with LLM");
            return (null, null);
        }
    }

    private async Task<string> GenerateOutcomeSummaryAsync(
        DecisionSpec spec,
        OutcomeDefinition outcome,
        Dictionary<string, object> knownTraits)
    {
        try
        {
            var knownTraitsSummary = BuildKnownTraitsSummary(knownTraits);
            
            var systemPrompt = $@"You are a decision helper.
Safety Guidelines: {spec.SafetyPreamble}

Context:
The user has provided the following information:
{knownTraitsSummary}

Your task: 
Provide a short, friendly summary explaining why the selected outcome is the best match for the user, referencing their specific traits (e.g., group size, ages, preferences).

Selected Outcome:
{outcome.OutcomeId}: {outcome.CareTypeMessage}

Format your response as a single paragraph.";

            var userPrompt = "Please explain why this recommendation is a good fit.";

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 200,
                Temperature = 0.5f
            };

            _logger.LogDebug("Requesting OpenAI to generate outcome summary");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                return response.Content.Trim();
            }
            
            return "Based on your responses, this is the best recommendation.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating outcome summary");
            return "Based on your responses, this is the best recommendation.";
        }
    }

    private class ClarifierResult
    {
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string QuestionType { get; set; } = "enum"; // Default to single select
    }

    private async Task<ClarifierResult?> GenerateClarifyingQuestionAsync(
        DecisionSpec spec,
        List<OutcomeDefinition> tiedOutcomes,
        Dictionary<string, object> knownTraits)
    {
        try
        {
            var outcomeSummaries = BuildOutcomeSummaries(tiedOutcomes);
            var knownTraitsSummary = BuildKnownTraitsSummary(knownTraits);
            
            var systemPrompt = $@"You are a helpful assistant that generates clarifying questions for a decision-making system.

Safety Guidelines: {spec.SafetyPreamble}

Context:
The user has already provided the following information:
{knownTraitsSummary}

Your task: 
1. Generate ONE clear, neutral question that will help distinguish between the provided outcome options.
2. Decide the best format for the question:
   - 'text': Open-ended free text (use when specific details are needed)
   - 'enum': Single selection from options (use when choices are mutually exclusive)
   - 'enum_list': Multiple selection from options (use when multiple choices are valid)
3. If using 'enum' or 'enum_list', provide 2-5 short, distinct options.

Format your response exactly as:
QUESTION: [The question text]
TYPE: [text | enum | enum_list]
OPTIONS: [Option 1], [Option 2], [Option 3] (Leave empty if TYPE is text)

- Do NOT ask for information that is already known.
- The question should be easy to understand.
- Options should be short phrases (1-3 words).";

            var userPrompt = spec.TieStrategy?.LlmPromptTemplate ?? 
                "Given candidate outcomes: {{summaries}} ask ONE neutral question to distinguish them. Output the question, type, and options.";
            
            userPrompt = userPrompt.Replace("{{summaries}}", outcomeSummaries);

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 250,
                Temperature = 0.7f
            };

            _logger.LogDebug("Requesting OpenAI to generate clarifying question");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var content = response.Content.Trim();
                var result = new ClarifierResult();
                
                // Parse the response
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase))
                    {
                        result.QuestionText = line.Substring(9).Trim();
                    }
                    else if (line.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                    {
                        var type = line.Substring(5).Trim().ToLower();
                        if (type == "text" || type == "enum" || type == "enum_list")
                        {
                            result.QuestionType = type;
                        }
                    }
                    else if (line.StartsWith("OPTIONS:", StringComparison.OrdinalIgnoreCase))
                    {
                        var optionsText = line.Substring(8).Trim();
                        if (!string.IsNullOrWhiteSpace(optionsText) && !optionsText.Equals("None", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Options = optionsText.Split(',')
                                .Select(o => o.Trim())
                                .Where(o => !string.IsNullOrWhiteSpace(o))
                                .ToList();
                        }
                    }
                }

                // Fallback if parsing failed but we have content
                if (string.IsNullOrEmpty(result.QuestionText) && !content.Contains("QUESTION:"))
                {
                    result.QuestionText = content;
                    result.QuestionType = "text"; // Default to text if format not followed
                }

                return result;
            }

            _logger.LogWarning("OpenAI failed to generate clarifying question: {Error}", response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating clarifying question");
            return null;
        }
    }

    private string BuildKnownTraitsSummary(Dictionary<string, object> knownTraits)
    {
        if (knownTraits == null || knownTraits.Count == 0) return "None";
        
        var sb = new StringBuilder();
        foreach (var kvp in knownTraits)
        {
            if (kvp.Key.StartsWith("llm_clarifier_")) continue;

            string valueStr;
            if (kvp.Value is List<int> intList)
                valueStr = string.Join(", ", intList);
            else if (kvp.Value is List<string> strList)
                valueStr = string.Join(", ", strList);
            else if (kvp.Value is System.Collections.IEnumerable && !(kvp.Value is string))
            {
                var items = new List<string>();
                foreach (var item in (System.Collections.IEnumerable)kvp.Value) items.Add(item?.ToString() ?? "");
                valueStr = string.Join(", ", items);
            }
            else
                valueStr = kvp.Value?.ToString() ?? "null";

            sb.AppendLine($"- {kvp.Key}: {valueStr}");
        }
        return sb.ToString();
    }

    private string BuildOutcomeSummaries(List<OutcomeDefinition> outcomes)
    {
        var sb = new StringBuilder();
        foreach (var outcome in outcomes)
        {
            sb.AppendLine($"- {outcome.OutcomeId}: {outcome.CareTypeMessage}");
        }
        return sb.ToString();
    }

    private Dictionary<string, object> ComputeDerivedTraits(DecisionSpec spec, Dictionary<string, object> knownTraits)
    {
        var allTraits = new Dictionary<string, object>(knownTraits);

        foreach (var derived in spec.DerivedTraits)
        {
            try
            {
                var value = EvaluateDerivedExpression(derived.Expression, knownTraits);
                if (value != null)
                {
                    allTraits[derived.Key] = value;
                    _logger.LogDebug("Derived trait {Key} = {Value}", derived.Key, value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute derived trait {Key}", derived.Key);
            }
        }

        return allTraits;
    }

    private object? EvaluateDerivedExpression(string expression, Dictionary<string, object> traits)
    {
        // Simple expression evaluator for common patterns
        if (expression.StartsWith("min("))
        {
            var traitKey = expression.Substring(4, expression.Length - 5);
            if (traits.TryGetValue(traitKey, out var value) && value is List<int> list)
            {
                return list.Count > 0 ? list.Min() : null;
            }
        }
        else if (expression.StartsWith("max("))
        {
            var traitKey = expression.Substring(4, expression.Length - 5);
            if (traits.TryGetValue(traitKey, out var value) && value is List<int> list)
            {
                return list.Count > 0 ? list.Max() : null;
            }
        }
        else if (expression.StartsWith("count(") && expression.Contains(">="))
        {
            // count(all_ages >= 18)
            var parts = expression.Substring(6, expression.Length - 7).Split(">=");
            var traitKey = parts[0].Trim();
            var threshold = int.Parse(parts[1].Trim());
            if (traits.TryGetValue(traitKey, out var value) && value is List<int> list)
            {
                return list.Count(x => x >= threshold);
            }
        }

        return null;
    }

    private bool EvaluateRule(string rule, Dictionary<string, object> traits)
    {
        try
        {
            // Simple rule evaluator: "trait_key operator value"
            var parts = rule.Split(new[] { "<=", ">=", "<", ">", "==" }, StringSplitOptions.None);
            if (parts.Length != 2) return false;

            var traitKey = parts[0].Trim();
            var expectedValueStr = parts[1].Trim();

            if (!traits.TryGetValue(traitKey, out var actualValue))
            {
                // Trait not yet known
                return false;
            }

            var op = rule.Contains(">=") ? ">=" :
                     rule.Contains("<=") ? "<=" :
                     rule.Contains("==") ? "==" :
                     rule.Contains(">") ? ">" :
                     rule.Contains("<") ? "<" : null;

            if (op == null) return false;

            if (actualValue is int actualInt && int.TryParse(expectedValueStr, out var expectedInt))
            {
                return op switch
                {
                    ">=" => actualInt >= expectedInt,
                    "<=" => actualInt <= expectedInt,
                    "==" => actualInt == expectedInt,
                    ">" => actualInt > expectedInt,
                    "<" => actualInt < expectedInt,
                    _ => false
                };
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate rule: {Rule}", rule);
            return false;
        }
    }

    private TraitDefinition? DetermineNextTrait(DecisionSpec spec, Dictionary<string, object> knownTraits)
    {
        // Return first required trait that is not yet known and whose dependencies are met
        foreach (var trait in spec.Traits.Where(t => t.Required && !t.IsPseudoTrait))
        {
            if (knownTraits.ContainsKey(trait.Key))
                continue;

            // Check dependencies
            if (trait.DependsOn.Any(dep => !knownTraits.ContainsKey(dep)))
                continue;

            return trait;
        }

        // Fallback to disambiguation order
        foreach (var traitKey in spec.Disambiguation.FallbackTraitOrder)
        {
            if (!knownTraits.ContainsKey(traitKey))
            {
                return spec.Traits.FirstOrDefault(t => t.Key == traitKey);
            }
        }

        return null;
    }
}
