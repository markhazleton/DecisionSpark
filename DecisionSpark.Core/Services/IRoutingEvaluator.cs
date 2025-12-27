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
            var clarifyingQuestion = await GenerateClarifyingQuestionAsync(spec, tiedOutcomes);
            if (!string.IsNullOrEmpty(clarifyingQuestion))
            {
                _logger.LogInformation("Generated LLM clarifying question for tie");
                // Create a dynamic pseudo-trait for this clarifier
                var dynamicTrait = new TraitDefinition
                {
                    Key = $"llm_clarifier_{Guid.NewGuid():N}",
                    QuestionText = clarifyingQuestion,
                    AnswerType = "enum",
                    ParseHint = "User's preference to resolve tie",
                    Required = false,
                    IsPseudoTrait = true
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

    private async Task<string?> GenerateClarifyingQuestionAsync(
        DecisionSpec spec,
        List<OutcomeDefinition> tiedOutcomes)
    {
        try
        {
            var outcomeSummaries = BuildOutcomeSummaries(tiedOutcomes);
            
            var systemPrompt = $@"You are a helpful assistant that generates clarifying questions for a decision-making system.

Safety Guidelines: {spec.SafetyPreamble}

Your task: Generate ONE clear, neutral question that will help distinguish between the provided outcome options.
The question should be easy to understand and help the user make a choice.

Return ONLY the question text, nothing else.";

            var userPrompt = spec.TieStrategy?.LlmPromptTemplate ?? 
                "Given candidate outcomes: {{summaries}} ask ONE neutral question to distinguish them. Output only the question.";
            
            userPrompt = userPrompt.Replace("{{summaries}}", outcomeSummaries);

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 150,
                Temperature = 0.7f
            };

            _logger.LogDebug("Requesting OpenAI to generate clarifying question");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                return response.Content.Trim();
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
