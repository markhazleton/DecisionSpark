using DecisionSpark.Core.Models.Spec;

namespace DecisionSpark.Core.Models.Runtime;

public class EvaluationResult
{
 public bool IsComplete { get; set; }
    public OutcomeDefinition? Outcome { get; set; }
    public string? NextTraitKey { get; set; }
    public TraitDefinition? NextTraitDefinition { get; set; }
    public bool RequiresClarifier { get; set; }
    public List<OutcomeDefinition> TiedOutcomes { get; set; } = new();
    public string ResolutionMode { get; set; } = string.Empty;
    public string? FinalSummary { get; set; }
}
