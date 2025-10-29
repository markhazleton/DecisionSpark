namespace DecisionSpark.Models.Spec;

public class DecisionSpec
{
    public string SpecId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string CanonicalBaseUrl { get; set; } = string.Empty;
    public string SafetyPreamble { get; set; } = string.Empty;
  public List<TraitDefinition> Traits { get; set; } = new();
    public List<DerivedTraitDefinition> DerivedTraits { get; set; } = new();
    public List<ImmediateSelectRule> ImmediateSelectIf { get; set; } = new();
    public List<OutcomeDefinition> Outcomes { get; set; } = new();
    public TieStrategy TieStrategy { get; set; } = new();
    public Disambiguation Disambiguation { get; set; } = new();
}

public class TraitDefinition
{
    public string Key { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string AnswerType { get; set; } = string.Empty;
    public string ParseHint { get; set; } = string.Empty;
    public bool Required { get; set; }
 public bool IsPseudoTrait { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public TraitBounds? Bounds { get; set; }
    public List<string>? Options { get; set; }
    public Dictionary<string, List<string>>? Mapping { get; set; }
}

public class TraitBounds
{
    public int Min { get; set; }
    public int Max { get; set; }
}

public class DerivedTraitDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

public class ImmediateSelectRule
{
    public string OutcomeId { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
}

public class OutcomeDefinition
{
    public string OutcomeId { get; set; } = string.Empty;
    public List<string> SelectionRules { get; set; } = new();
    public List<DisplayCard> DisplayCards { get; set; } = new();
    public string CareTypeMessage { get; set; } = string.Empty;
    public FinalResultDefinition FinalResult { get; set; } = new();
}

public class DisplayCard
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CareTypeMessage { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public List<string> BodyText { get; set; } = new();
    public List<string> CareTypeDetails { get; set; } = new();
    public List<string> Rules { get; set; } = new();
}

public class FinalResultDefinition
{
public string ResolutionButtonLabel { get; set; } = string.Empty;
    public string ResolutionButtonUrl { get; set; } = string.Empty;
    public string AnalyticsResolutionCode { get; set; } = string.Empty;
}

public class TieStrategy
{
    public string Mode { get; set; } = "LLM_CLARIFIER";
    public int ClarifierMaxAttempts { get; set; } = 2;
    public List<TraitDefinition> PseudoTraits { get; set; } = new();
    public string LlmPromptTemplate { get; set; } = string.Empty;
}

public class Disambiguation
{
    public List<string> FallbackTraitOrder { get; set; } = new();
}
