using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DecisionSpark.Models.Api.DecisionSpecs;

/// <summary>
/// Request to create a new DecisionSpec.
/// </summary>
public class DecisionSpecCreateRequest
{
    [Required]
    [JsonPropertyName("spec_id")]
    public string SpecId { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d+\.\d+\.\d+$", ErrorMessage = "Version must match format: major.minor.patch")]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";

    [Required]
    [JsonPropertyName("metadata")]
    public DecisionSpecMetadataDto Metadata { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one trait is required")]
    [JsonPropertyName("traits")]
    public List<TraitDto> Traits { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "At least one outcome is required")]
    [JsonPropertyName("outcomes")]
    public List<OutcomeDto> Outcomes { get; set; } = new();
}

/// <summary>
/// Response containing a list of DecisionSpec summaries.
/// </summary>
public class DecisionSpecListResponse
{
    [JsonPropertyName("items")]
    public List<DecisionSpecSummaryDto> Items { get; set; } = new();
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("page")]
    public int Page { get; set; }
    
    [JsonPropertyName("page_size")]
    public int PageSize { get; set; }
}

/// <summary>
/// Summary information for a DecisionSpec (used in list views).
/// </summary>
public class DecisionSpecSummaryDto
{
    [JsonPropertyName("spec_id")]
    public string SpecId { get; set; } = string.Empty;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("trait_count")]
    public int TraitCount { get; set; }
    
    [JsonPropertyName("has_unverified_draft")]
    public bool HasUnverifiedDraft { get; set; }
}

/// <summary>
/// Complete DecisionSpec document.
/// </summary>
public class DecisionSpecDocumentDto
{
    [Required]
    [JsonPropertyName("spec_id")]
    public string SpecId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";

    [Required]
    [JsonPropertyName("metadata")]
    public DecisionSpecMetadataDto Metadata { get; set; } = new();

    [Required]
    [JsonPropertyName("traits")]
    public List<TraitDto> Traits { get; set; } = new();

    [Required]
    [JsonPropertyName("outcomes")]
    public List<OutcomeDto> Outcomes { get; set; } = new();

    [JsonPropertyName("audit")]
    public List<AuditEventDto> Audit { get; set; } = new();
}

/// <summary>
/// Metadata for a DecisionSpec.
/// </summary>
public class DecisionSpecMetadataDto
{
    [Required]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Trait definition.
/// </summary>
public class TraitDto
{
    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("answer_type")]
    public string AnswerType { get; set; } = "choice";

    [Required]
    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("parse_hint")]
    public string? ParseHint { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("is_pseudo_trait")]
    public bool IsPseudoTrait { get; set; }

    [JsonPropertyName("allow_multiple")]
    public bool AllowMultiple { get; set; }

    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; set; }

    [JsonPropertyName("bounds")]
    public Dictionary<string, object>? Bounds { get; set; }

    [JsonPropertyName("options")]
    public List<OptionDto> Options { get; set; } = new();

    [JsonPropertyName("mapping")]
    public Dictionary<string, List<string>>? Mapping { get; set; }

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Option for a trait.
/// </summary>
public class OptionDto
{
    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Outcome definition.
/// </summary>
public class OutcomeDto
{
    [Required]
    [JsonPropertyName("outcome_id")]
    public string OutcomeId { get; set; } = string.Empty;

    [JsonPropertyName("selection_rules")]
    public List<string> SelectionRules { get; set; } = new();

    [JsonPropertyName("display_cards")]
    public List<object> DisplayCards { get; set; } = new();
}

/// <summary>
/// Request to patch a single trait.
/// </summary>
public class TraitPatchRequest
{
    [JsonPropertyName("question_text")]
    public string? QuestionText { get; set; }
    
    [JsonPropertyName("parse_hint")]
    public string? ParseHint { get; set; }
    
    [JsonPropertyName("options")]
    public List<OptionDto>? Options { get; set; }
    
    [JsonPropertyName("bounds")]
    public Dictionary<string, object>? Bounds { get; set; }
    
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Response containing audit history.
/// </summary>
public class AuditLogResponse
{
    [JsonPropertyName("spec_id")]
    public string SpecId { get; set; } = string.Empty;
    
    [JsonPropertyName("events")]
    public List<AuditEventDto> Events { get; set; } = new();
}

/// <summary>
/// Audit event entry.
/// </summary>
public class AuditEventDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("actor")]
    public string Actor { get; set; } = string.Empty;
    
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Request to generate an LLM draft.
/// </summary>
public class LlmDraftRequest
{
    [Required]
    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = string.Empty;

    [JsonPropertyName("tone")]
    public string? Tone { get; set; }

    [JsonPropertyName("seed_spec_id")]
    public string? SeedSpecId { get; set; }
}

/// <summary>
/// Response for an LLM draft.
/// </summary>
public class LlmDraftResponse
{
    [JsonPropertyName("draft_id")]
    public string DraftId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Pending";
    
    [JsonPropertyName("spec")]
    public DecisionSpecDocumentDto? Spec { get; set; }
    
    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Request to transition a DecisionSpec to a new lifecycle status.
/// </summary>
public class StatusTransitionRequest
{
    [Required]
    [JsonPropertyName("new_status")]
    public string NewStatus { get; set; } = string.Empty;
    
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}
