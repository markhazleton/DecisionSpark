using System.ComponentModel.DataAnnotations;

namespace DecisionSpark.Areas.Admin.ViewModels.DecisionSpecs;

/// <summary>
/// View model for the DecisionSpec catalog/list view.
/// </summary>
public class DecisionSpecListViewModel
{
    public List<DecisionSpecSummaryViewModel> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public string? OwnerFilter { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

/// <summary>
/// Summary view model for DecisionSpec list items.
/// </summary>
public class DecisionSpecSummaryViewModel
{
    public string SpecId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public int QuestionCount { get; set; }
    public bool HasUnverifiedDraft { get; set; }

    public string StatusBadgeClass => Status switch
    {
        "Draft" => "badge bg-secondary",
        "InReview" => "badge bg-warning",
        "Published" => "badge bg-success",
        "Retired" => "badge bg-dark",
        _ => "badge bg-light"
    };
}

/// <summary>
/// View model for creating/editing a DecisionSpec.
/// </summary>
public class DecisionSpecEditViewModel
{
    [Required(ErrorMessage = "Spec ID is required")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Spec ID must contain only letters, numbers, underscores, and hyphens")]
    public string SpecId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Version is required")]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Version must follow format: major.minor.patch (e.g., 2025.12.1)")]
    public string Version { get; set; } = "2025.12.1";

    public string Status { get; set; } = "Draft";

    [Url(ErrorMessage = "Canonical Base URL must be a valid URL")]
    public string? CanonicalBaseUrl { get; set; }

    [StringLength(2000, ErrorMessage = "Safety preamble cannot exceed 2000 characters")]
    public string? SafetyPreamble { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency control. Empty for new specs.
    /// </summary>
    public string ETag { get; set; } = string.Empty;

    /// <summary>
    /// Flag to show concurrency conflict UI elements.
    /// </summary>
    public bool ShowConcurrencyConflict { get; set; }

    [Required]
    public DecisionSpecMetadataViewModel Metadata { get; set; } = new();

    [Required(ErrorMessage = "At least one question is required")]
    [MinLength(1, ErrorMessage = "At least one question is required")]
    public List<QuestionViewModel> Questions { get; set; } = new();

    [Required(ErrorMessage = "At least one outcome is required")]
    [MinLength(1, ErrorMessage = "At least one outcome is required")]
    public List<OutcomeViewModel> Outcomes { get; set; } = new();

    public List<DerivedTraitViewModel> DerivedTraits { get; set; } = new();
    public List<ImmediateSelectRuleViewModel> ImmediateSelectRules { get; set; } = new();
    public TieStrategyViewModel? TieStrategy { get; set; }
    public DisambiguationViewModel? Disambiguation { get; set; }

    public bool IsNewSpec => string.IsNullOrWhiteSpace(ETag);
}

/// <summary>
/// View model for DecisionSpec metadata.
/// </summary>
public class DecisionSpecMetadataViewModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, ErrorMessage = "Name cannot exceed 200 characters")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// View model for a question in the DecisionSpec.
/// </summary>
public class QuestionViewModel
{
    [Required(ErrorMessage = "Question ID is required")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Question ID must contain only letters, numbers, underscores, and hyphens")]
    public string QuestionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Question type is required")]
    public string Type { get; set; } = "SingleSelect";

    [Required(ErrorMessage = "Question prompt is required")]
    [StringLength(500, ErrorMessage = "Prompt cannot exceed 500 characters")]
    public string Prompt { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Help text cannot exceed 1000 characters")]
    public string? HelpText { get; set; }

    [StringLength(2000, ErrorMessage = "Parse hint cannot exceed 2000 characters")]
    public string? ParseHint { get; set; }

    public bool Required { get; set; } = true;
    public bool IsPseudoTrait { get; set; } = false;
    public bool AllowMultiple { get; set; } = false;

    public List<string> DependsOn { get; set; } = new();
    public List<OptionViewModel> Options { get; set; } = new();

    public Dictionary<string, object>? Validation { get; set; }
    public QuestionBoundsViewModel? Bounds { get; set; }

    [StringLength(500, ErrorMessage = "Comment cannot exceed 500 characters")]
    public string? Comment { get; set; }

    /// <summary>
    /// Mapping for pseudo-trait tie-breaking logic.
    /// JSON format: Dictionary<string, List<string>> mapping outcome IDs to option lists.
    /// </summary>
    public Dictionary<string, List<string>>? Mapping { get; set; }
}

/// <summary>
/// View model for an option in a question.
/// </summary>
public class OptionViewModel
{
    [Required(ErrorMessage = "Option ID is required")]
    public string OptionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Option label is required")]
    [StringLength(200, ErrorMessage = "Label cannot exceed 200 characters")]
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? NextQuestionId { get; set; }
}

/// <summary>
/// View model for an outcome in the DecisionSpec.
/// </summary>
public class OutcomeViewModel
{
    [Required(ErrorMessage = "Outcome ID is required")]
    [RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Outcome ID must contain only letters, numbers, underscores, and hyphens")]
    public string OutcomeId { get; set; } = string.Empty;

    public List<string> SelectionRules { get; set; } = new();

    [StringLength(1000, ErrorMessage = "Care type message cannot exceed 1000 characters")]
    public string? CareTypeMessage { get; set; }

    public List<OutcomeDisplayCardViewModel> DisplayCards { get; set; } = new();
    public OutcomeFinalResultViewModel? FinalResult { get; set; }
}

/// <summary>
/// View model for an outcome display card.
/// </summary>
public class OutcomeDisplayCardViewModel
{
    [Required(ErrorMessage = "Card title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Subtitle cannot exceed 200 characters")]
    public string? Subtitle { get; set; }

    public string? GroupId { get; set; }

    [StringLength(1000, ErrorMessage = "Care type message cannot exceed 1000 characters")]
    public string? CareTypeMessage { get; set; }

    [Url(ErrorMessage = "Icon URL must be a valid URL")]
    public string? IconUrl { get; set; }

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;

    public List<string> BodyText { get; set; } = new();
    public List<string> CareTypeDetails { get; set; } = new();
    public List<string> Rules { get; set; } = new();
}

/// <summary>
/// View model for question bounds
/// </summary>
public class QuestionBoundsViewModel
{
    public int? Min { get; set; }
    public int? Max { get; set; }
}

/// <summary>
/// View model for derived traits
/// </summary>
public class DerivedTraitViewModel
{
    [Required(ErrorMessage = "Trait key is required")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "Expression is required")]
    [StringLength(1000, ErrorMessage = "Expression cannot exceed 1000 characters")]
    public string Expression { get; set; } = string.Empty;
}

/// <summary>
/// View model for immediate select rules
/// </summary>
public class ImmediateSelectRuleViewModel
{
    [Required(ErrorMessage = "Outcome ID is required")]
    public string OutcomeId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rule is required")]
    [StringLength(1000, ErrorMessage = "Rule cannot exceed 1000 characters")]
    public string Rule { get; set; } = string.Empty;
}

/// <summary>
/// View model for tie strategy
/// </summary>
public class TieStrategyViewModel
{
    [Required(ErrorMessage = "Mode is required")]
    public string Mode { get; set; } = "LLM_CLARIFIER";

    [Range(1, 10, ErrorMessage = "Clarifier max attempts must be between 1 and 10")]
    public int ClarifierMaxAttempts { get; set; } = 3;

    public List<QuestionViewModel> PseudoTraits { get; set; } = new();

    [StringLength(2000, ErrorMessage = "LLM prompt template cannot exceed 2000 characters")]
    public string? LlmPromptTemplate { get; set; }
}

/// <summary>
/// View model for disambiguation
/// </summary>
public class DisambiguationViewModel
{
    public List<string> FallbackTraitOrder { get; set; } = new();
}

/// <summary>
/// View model for outcome final result
/// </summary>
public class OutcomeFinalResultViewModel
{
    [StringLength(200, ErrorMessage = "Resolution button label cannot exceed 200 characters")]
    public string? ResolutionButtonLabel { get; set; }

    [Url(ErrorMessage = "Resolution button URL must be a valid URL")]
    public string? ResolutionButtonUrl { get; set; }

    [StringLength(100, ErrorMessage = "Analytics resolution code cannot exceed 100 characters")]
    public string? AnalyticsResolutionCode { get; set; }
}

/// <summary>
/// View model for DecisionSpec details page.
/// </summary>
public class DecisionSpecDetailsViewModel
{
    public string SpecId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public DecisionSpecMetadataViewModel Metadata { get; set; } = new();
    public int QuestionCount { get; set; }
    public int OutcomeCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
    public List<AuditEventViewModel> AuditHistory { get; set; } = new();

    public string StatusBadgeClass => Status switch
    {
        "Draft" => "badge bg-secondary",
        "InReview" => "badge bg-warning",
        "Published" => "badge bg-success",
        "Retired" => "badge bg-dark",
        _ => "badge bg-light"
    };

    public List<string> AvailableTransitions => Status switch
    {
        "Draft" => new List<string> { "InReview", "Retired" },
        "InReview" => new List<string> { "Draft", "Published" },
        "Published" => new List<string> { "InReview", "Retired" },
        "Retired" => new List<string>(),
        _ => new List<string>()
    };
}

/// <summary>
/// View model for audit event entries.
/// </summary>
public class AuditEventViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public string ActionBadgeClass => Action switch
    {
        "Created" => "badge bg-primary",
        "Updated" => "badge bg-info",
        "QuestionPatched" => "badge bg-warning",
        "Deleted" => "badge bg-danger",
        "Restored" => "badge bg-success",
        "LLMDraft" => "badge bg-purple",
        _ => "badge bg-secondary"
    };
}
