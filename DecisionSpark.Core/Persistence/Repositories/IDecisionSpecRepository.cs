using DecisionSpark.Core.Models.Spec;

namespace DecisionSpark.Core.Persistence.Repositories;

/// <summary>
/// Repository contract for DecisionSpec CRUD, soft delete, restore, and audit operations.
/// </summary>
public interface IDecisionSpecRepository
{
    /// <summary>
    /// Creates a new DecisionSpec.
    /// </summary>
    Task<(DecisionSpecDocument Document, string ETag)> CreateAsync(DecisionSpecDocument spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a DecisionSpec by ID and version.
    /// </summary>
    Task<(DecisionSpecDocument Document, string ETag)?> GetAsync(string specId, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing DecisionSpec with optimistic concurrency check.
    /// </summary>
    Task<(DecisionSpecDocument Document, string ETag)?> UpdateAsync(string specId, DecisionSpecDocument spec, string ifMatchETag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a DecisionSpec (moves to archive).
    /// </summary>
    Task<bool> DeleteAsync(string specId, string version, string ifMatchETag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted DecisionSpec.
    /// </summary>
    Task<(DecisionSpecDocument Document, string ETag)?> RestoreAsync(string specId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists DecisionSpecs with optional filters.
    /// </summary>
    Task<IEnumerable<DecisionSpecSummary>> ListAsync(string? status = null, string? owner = null, string? searchTerm = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full document optimized for runtime consumption.
    /// </summary>
    Task<string?> GetFullDocumentJsonAsync(string specId, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an audit entry for a spec.
    /// </summary>
    Task AppendAuditEntryAsync(string specId, AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves audit history for a spec.
    /// </summary>
    Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(string specId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary information for listing DecisionSpecs.
/// </summary>
public class DecisionSpecSummary
{
    public string SpecId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public int QuestionCount { get; set; }
    public bool HasUnverifiedDraft { get; set; }
    public string ETag { get; set; } = string.Empty;
}

/// <summary>
/// Audit entry for spec changes.
/// </summary>
public class AuditEntry
{
    public string AuditId { get; set; } = Guid.NewGuid().ToString();
    public string SpecId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PayloadSnapshot { get; set; }
}
