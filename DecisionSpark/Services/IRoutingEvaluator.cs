using DecisionSpark.Models.Runtime;
using DecisionSpark.Models.Spec;

namespace DecisionSpark.Services;

public interface IRoutingEvaluator
{
    Task<EvaluationResult> EvaluateAsync(DecisionSpec spec, Dictionary<string, object> knownTraits);
}

public class RoutingEvaluator : IRoutingEvaluator
{
    private readonly ILogger<RoutingEvaluator> _logger;

    public RoutingEvaluator(ILogger<RoutingEvaluator> logger)
    {
        _logger = logger;
    }

    public Task<EvaluationResult> EvaluateAsync(DecisionSpec spec, Dictionary<string, object> knownTraits)
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
          return Task.FromResult(new EvaluationResult
         {
            IsComplete = true,
              Outcome = outcome,
          ResolutionMode = "IMMEDIATE"
      });
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
            return Task.FromResult(new EvaluationResult
         {
     IsComplete = true,
 Outcome = satisfiedOutcomes[0],
 ResolutionMode = "SINGLE_MATCH"
    });
   }

        // If multiple outcomes satisfied, return tie
     if (satisfiedOutcomes.Count > 1)
        {
  _logger.LogInformation("Tie detected: {Count} outcomes", satisfiedOutcomes.Count);
        return Task.FromResult(new EvaluationResult
   {
     IsComplete = false,
     RequiresClarifier = true,
   TiedOutcomes = satisfiedOutcomes
            });
        }

        // No outcome yet, determine next trait to ask
        var nextTrait = DetermineNextTrait(spec, knownTraits);
        if (nextTrait != null)
        {
  _logger.LogDebug("Next trait to collect: {TraitKey}", nextTrait.Key);
    return Task.FromResult(new EvaluationResult
      {
                IsComplete = false,
     NextTraitKey = nextTrait.Key,
      NextTraitDefinition = nextTrait
        });
      }

  // No traits left but no outcome - should not happen with valid spec
    _logger.LogWarning("No outcome and no next trait - defaulting to first outcome");
        return Task.FromResult(new EvaluationResult
        {
 IsComplete = true,
      Outcome = spec.Outcomes.First(),
            ResolutionMode = "FALLBACK"
        });
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
