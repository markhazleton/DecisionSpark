using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace DecisionSpark.Core.Services;

/// <summary>
/// Service for patching individual questions within a DecisionSpec.
/// </summary>
public class QuestionPatchService
{
    private readonly IDecisionSpecRepository _repository;
    private readonly ILogger<QuestionPatchService> _logger;

    public QuestionPatchService(
        IDecisionSpecRepository repository,
        ILogger<QuestionPatchService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Patches a specific question within a DecisionSpec.
    /// </summary>
    public async Task<(DecisionSpecDocument Document, string ETag)?> PatchQuestionAsync(
        string specId,
        string questionId,
        string? prompt,
        string? helpText,
        List<Option>? options,
        Dictionary<string, object>? validation,
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

        // Find and patch the question
        var question = doc.Questions.FirstOrDefault(q => q.QuestionId == questionId);
        if (question == null)
        {
            throw new KeyNotFoundException($"Question {questionId} not found in spec {specId}");
        }

        // Apply patches
        if (prompt != null)
        {
            question.Prompt = prompt;
        }

        if (helpText != null)
        {
            question.HelpText = helpText;
        }

        if (options != null)
        {
            question.Options = options;
        }

        if (validation != null)
        {
            question.Validation = validation;
        }

        // Update the spec
        var result = await _repository.UpdateAsync(specId, doc, ifMatchETag, cancellationToken);

        if (result != null)
        {
            // Append audit entry
            await _repository.AppendAuditEntryAsync(specId, new AuditEntry
            {
                SpecId = specId,
                Action = "QuestionPatched",
                Summary = $"Patched question {questionId}",
                Actor = actor,
                Source = "API"
            }, cancellationToken);

            _logger.LogInformation("Patched question {QuestionId} in spec {SpecId}", questionId, specId);
        }

        return result;
    }
}
