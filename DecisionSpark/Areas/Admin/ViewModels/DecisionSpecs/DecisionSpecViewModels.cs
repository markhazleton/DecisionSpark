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

    public bool Required { get; set; } = true;

    public List<OptionViewModel> Options { get; set; } = new();

    public Dictionary<string, object>? Validation { get; set; }
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

    public List<OutcomeDisplayCardViewModel> DisplayCards { get; set; } = new();
}

/// <summary>
/// View model for an outcome display card.
/// </summary>
public class OutcomeDisplayCardViewModel
{
    [Required(ErrorMessage = "Card title is required")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Description { get; set; } = string.Empty;
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
