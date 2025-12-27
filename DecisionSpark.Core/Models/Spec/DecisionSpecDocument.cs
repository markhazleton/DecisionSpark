using System.Text.Json.Serialization;

namespace DecisionSpark.Core.Models.Spec;

/// <summary>
/// DecisionSpec document structure for CRUD operations.
/// Uses IDENTICAL schema to runtime DecisionSpec - just inherits from it.
/// Lifecycle metadata stored separately in sidecar files.
/// </summary>
public class DecisionSpecDocument : DecisionSpec
{
    // Lifecycle properties NOT part of runtime schema
    // These are populated from sidecar metadata files
    public string Status { get; set; } = "Draft";
    public DecisionSpecMetadata? Metadata { get; set; }
}

/// <summary>
/// Lifecycle metadata for DecisionSpec management (stored separately in sidecar file).
/// NOT part of runtime schema.
/// </summary>
public class DecisionSpecMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;
    
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Draft";
    
    [JsonPropertyName("unverified")]
    public bool Unverified { get; set; } = false;
    
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
    
    [JsonPropertyName("updated_by")]
    public string UpdatedBy { get; set; } = string.Empty;
    
    [JsonPropertyName("soft_deleted_at")]
    public DateTimeOffset? SoftDeletedAt { get; set; }
    
    [JsonPropertyName("restorable_until")]
    public DateTimeOffset? RestorableUntil { get; set; }
}
