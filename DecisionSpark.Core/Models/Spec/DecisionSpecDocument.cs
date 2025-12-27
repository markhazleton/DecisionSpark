namespace DecisionSpark.Core.Models.Spec;

/// <summary>
/// Extended DecisionSpec document structure for CRUD operations.
/// Contains questions and outcomes for simplified question-based decision trees.
/// </summary>
public class DecisionSpecDocument
{
    public string SpecId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Status { get; set; } = "Draft";
    public DecisionSpecMetadata Metadata { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public List<Outcome> Outcomes { get; set; } = new();
}

/// <summary>
/// Metadata for DecisionSpec management and lifecycle.
/// </summary>
public class DecisionSpecMetadata
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public bool Unverified { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Question structure for new question-type feature (simpler than TraitDefinition).
/// </summary>
public class Question
{
    public string QuestionId { get; set; } = string.Empty;
    public string Type { get; set; } = "SingleSelect";
    public string Prompt { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public bool Required { get; set; } = true;
    public List<Option> Options { get; set; } = new();
    public Dictionary<string, object>? Validation { get; set; }
}

/// <summary>
/// Option for a question.
/// </summary>
public class Option
{
    public string OptionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? NextQuestionId { get; set; }
}

/// <summary>
/// Outcome with simplified question-based rules.
/// </summary>
public class Outcome
{
    public string OutcomeId { get; set; } = string.Empty;
    public List<string> SelectionRules { get; set; } = new();
    public List<OutcomeDisplayCard> DisplayCards { get; set; } = new();
}

/// <summary>
/// Display card for outcomes (simpler version for question-type feature).
/// </summary>
public class OutcomeDisplayCard
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
