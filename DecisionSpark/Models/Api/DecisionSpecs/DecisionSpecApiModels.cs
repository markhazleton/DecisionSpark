using System.ComponentModel.DataAnnotations;

namespace DecisionSpark.Models.Api.DecisionSpecs;

/// <summary>
/// Request to create a new DecisionSpec.
/// </summary>
public class DecisionSpecCreateRequest
{
    [Required]
    public string SpecId { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Version must match format: major.minor.patch")]
    public string Version { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    [Required]
    public DecisionSpecMetadataDto Metadata { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one question is required")]
    public List<QuestionDto> Questions { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one outcome is required")]
    public List<OutcomeDto> Outcomes { get; set; } = new();
}

/// <summary>
/// Response containing a list of DecisionSpec summaries.
/// </summary>
public class DecisionSpecListResponse
{
    public List<DecisionSpecSummaryDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Summary information for a DecisionSpec (used in list views).
/// </summary>
public class DecisionSpecSummaryDto
{
    public string SpecId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public int QuestionCount { get; set; }
    public bool HasUnverifiedDraft { get; set; }
}

/// <summary>
/// Complete DecisionSpec document.
/// </summary>
public class DecisionSpecDocumentDto
{
    [Required]
    public string SpecId { get; set; } = string.Empty;

    [Required]
    public string Version { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    [Required]
    public DecisionSpecMetadataDto Metadata { get; set; } = new();

    [Required]
    public List<QuestionDto> Questions { get; set; } = new();

    [Required]
    public List<OutcomeDto> Outcomes { get; set; } = new();

    public List<AuditEventDto> Audit { get; set; } = new();
}

/// <summary>
/// Metadata for a DecisionSpec.
/// </summary>
public class DecisionSpecMetadataDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Question definition.
/// </summary>
public class QuestionDto
{
    [Required]
    public string QuestionId { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = "SingleSelect";

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public string? HelpText { get; set; }

    public bool Required { get; set; } = true;

    public List<OptionDto> Options { get; set; } = new();

    public Dictionary<string, object>? Validation { get; set; }
}

/// <summary>
/// Option for a question.
/// </summary>
public class OptionDto
{
    [Required]
    public string OptionId { get; set; } = string.Empty;

    [Required]
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? NextQuestionId { get; set; }
}

/// <summary>
/// Outcome definition.
/// </summary>
public class OutcomeDto
{
    [Required]
    public string OutcomeId { get; set; } = string.Empty;

    public List<string> SelectionRules { get; set; } = new();

    public List<object> DisplayCards { get; set; } = new();
}

/// <summary>
/// Request to patch a single question.
/// </summary>
public class QuestionPatchRequest
{
    public string? Prompt { get; set; }
    public string? HelpText { get; set; }
    public List<OptionDto>? Options { get; set; }
    public Dictionary<string, object>? Validation { get; set; }
}

/// <summary>
/// Response containing audit history.
/// </summary>
public class AuditLogResponse
{
    public string SpecId { get; set; } = string.Empty;
    public List<AuditEventDto> Events { get; set; } = new();
}

/// <summary>
/// Audit event entry.
/// </summary>
public class AuditEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Request to generate an LLM draft.
/// </summary>
public class LlmDraftRequest
{
    [Required]
    public string Instruction { get; set; } = string.Empty;

    public string? Tone { get; set; }

    public string? SeedSpecId { get; set; }
}

/// <summary>
/// Response for an LLM draft.
/// </summary>
public class LlmDraftResponse
{
    public string DraftId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DecisionSpecDocumentDto? Spec { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Request to transition a DecisionSpec to a new lifecycle status.
/// </summary>
public class StatusTransitionRequest
{
    [Required]
    public string NewStatus { get; set; } = string.Empty;
    
    public string? Comment { get; set; }
}
