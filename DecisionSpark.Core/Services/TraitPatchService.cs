using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace DecisionSpark.Core.Services;

/// <summary>
/// Service for patching individual traits within a DecisionSpec.
/// </summary>
public class TraitPatchService
{
    private readonly IDecisionSpecRepository _repository;
    private readonly ILogger<TraitPatchService> _logger;

    public TraitPatchService(
        IDecisionSpecRepository repository,
        ILogger<TraitPatchService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Patches a specific trait within a DecisionSpec.
    /// </summary>
    public virtual async Task<(DecisionSpecDocument Document, string ETag)?> PatchTraitAsync(
        string specId,
        string traitKey,
        string? questionText,
        string? parseHint,
        List<string>? options,
        Dictionary<string, object>? bounds,
        string? comment,
        string ifMatchETag,
        string actor = "System",
        CancellationToken cancellationToken = default)
    {
        // Get current spec
        var current = await _repository.GetAsync(specId, null, cancellationToken);
        if (current == null)
        {
            return null;
        }

        var (doc, currentETag) = current.Value;

        // Verify ETag
        if (currentETag != ifMatchETag)
        {
            throw new InvalidOperationException("ETag mismatch - concurrent modification detected");
        }

        // Find and patch the trait
        var trait = doc.Traits.FirstOrDefault(t => t.Key == traitKey);
        if (trait == null)
        {
            throw new KeyNotFoundException($"Trait {traitKey} not found in spec {specId}");
        }

        // Apply patches
        if (questionText != null)
        {
            trait.QuestionText = questionText;
        }

        if (parseHint != null)
        {
            trait.ParseHint = parseHint;
        }

        if (options != null)
        {
            trait.Options = options;
        }

        if (bounds != null)
        {
            // Convert bounds dictionary to TraitBounds if needed
            trait.Bounds = new TraitBounds
            {
                Min = bounds.TryGetValue("min", out var minVal) ? Convert.ToInt32(minVal) : 0,
                Max = bounds.TryGetValue("max", out var maxVal) ? Convert.ToInt32(maxVal) : int.MaxValue
            };
        }

        if (comment != null)
        {
            trait.Comment = comment;
        }

        // Update the spec
        var result = await _repository.UpdateAsync(specId, doc, ifMatchETag, cancellationToken);

        if (result != null)
        {
            // Append audit entry
            await _repository.AppendAuditEntryAsync(specId, new AuditEntry
            {
                SpecId = specId,
                Action = "TraitPatched",
                Summary = $"Patched trait {traitKey}",
                Actor = actor,
                Source = "API"
            }, cancellationToken);

            _logger.LogInformation("Patched trait {TraitKey} in spec {SpecId}", traitKey, specId);
        }

        return result;
    }
}
