using System.Text.Json;
using DecisionSpark.Core.Models.Configuration;
using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.FileStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionSpark.Core.Persistence.Repositories;

/// <summary>
/// File-based repository for DecisionSpec documents.
/// </summary>
public class DecisionSpecRepository : IDecisionSpecRepository
{
    private readonly DecisionSpecFileStore _fileStore;
    private readonly FileSearchIndexer _indexer;
    private readonly DecisionSpecsOptions _options;
    private readonly ILogger<DecisionSpecRepository> _logger;

    public DecisionSpecRepository(
        DecisionSpecFileStore fileStore,
        FileSearchIndexer indexer,
        IOptions<DecisionSpecsOptions> options,
        ILogger<DecisionSpecRepository> logger)
    {
        _fileStore = fileStore;
        _indexer = indexer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(DecisionSpecDocument Document, string ETag)> CreateAsync(DecisionSpecDocument spec, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(spec, GetSerializerOptions());
        var etag = await _fileStore.WriteAsync(spec.SpecId, spec.Version, spec.Status, json, cancellationToken);
        
        await _indexer.UpdateEntryAsync(spec.SpecId, spec.Version, spec.Status, cancellationToken);
        
        await AppendAuditEntryAsync(spec.SpecId, new AuditEntry
        {
            SpecId = spec.SpecId,
            Action = "Created",
            Summary = $"Created DecisionSpec {spec.SpecId} v{spec.Version}",
            Actor = "System",
            Source = "API"
        }, cancellationToken);

        _logger.LogInformation("Created DecisionSpec {SpecId} v{Version}", spec.SpecId, spec.Version);

        return (spec, etag);
    }

    public async Task<(DecisionSpecDocument Document, string ETag)?> GetAsync(string specId, string? version = null, CancellationToken cancellationToken = default)
    {
        // If no version specified, find the latest from index
        if (string.IsNullOrWhiteSpace(version))
        {
            var indexResults = await _indexer.QueryAsync(cancellationToken: cancellationToken);
            var entry = indexResults.FirstOrDefault(e => e.SpecId == specId);
            if (entry == null)
            {
                return null;
            }
            version = entry.Version;
        }

        // Try to find in all status folders (new format)
        foreach (var status in new[] { "Published", "Draft", "InReview", "Retired" })
        {
            var result = await _fileStore.ReadAsync(specId, version, status, cancellationToken);
            if (result != null)
            {
                var (content, etag) = result.Value;
                var doc = JsonSerializer.Deserialize<DecisionSpecDocument>(content, GetSerializerOptions());
                if (doc != null)
                {
                    return (doc, etag);
                }
            }
        }

        return null;
    }

    private static string ComputeETag(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    private static JsonSerializerOptions GetSerializerOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<(DecisionSpecDocument Document, string ETag)?> UpdateAsync(string specId, DecisionSpecDocument spec, string ifMatchETag, CancellationToken cancellationToken = default)
    {
        // Verify ETag matches current version
        var current = await GetAsync(specId, spec.Version, cancellationToken);
        if (current == null)
        {
            throw new InvalidOperationException($"DecisionSpec {specId} not found");
        }

        if (current.Value.ETag != ifMatchETag)
        {
            throw new InvalidOperationException("ETag mismatch - concurrent modification detected");
        }

        // Write updated spec
        var json = JsonSerializer.Serialize(spec, GetSerializerOptions());
        var etag = await _fileStore.WriteAsync(spec.SpecId, spec.Version, spec.Status, json, cancellationToken);
        
        await _indexer.UpdateEntryAsync(spec.SpecId, spec.Version, spec.Status, cancellationToken);
        
        await AppendAuditEntryAsync(spec.SpecId, new AuditEntry
        {
            SpecId = spec.SpecId,
            Action = "Updated",
            Summary = $"Updated DecisionSpec {spec.SpecId} v{spec.Version}",
            Actor = "System",
            Source = "API"
        }, cancellationToken);

        _logger.LogInformation("Updated DecisionSpec {SpecId} v{Version}", spec.SpecId, spec.Version);

        return (spec, etag);
    }

    public async Task<bool> DeleteAsync(string specId, string version, string ifMatchETag, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(specId, version, cancellationToken);
        if (current == null)
        {
            return false;
        }

        if (current.Value.ETag != ifMatchETag)
        {
            throw new InvalidOperationException("ETag mismatch - concurrent modification detected");
        }

        // Get audit path before deleting the spec file
        var auditPath = await GetAuditPathAsync(specId, cancellationToken);

        // Write the delete audit entry before moving to archive
        await AppendAuditEntryAsync(specId, new AuditEntry
        {
            SpecId = specId,
            Action = "Deleted",
            Summary = $"Soft-deleted DecisionSpec {specId} v{version}",
            Actor = "System",
            Source = "API"
        }, cancellationToken);

        var success = await _fileStore.SoftDeleteAsync(specId, version, current.Value.Document.Status, cancellationToken);
        
        if (success)
        {
            await _indexer.RemoveEntryAsync(specId, cancellationToken);
            
            // Move audit log to archive as well
            if (auditPath != null && File.Exists(auditPath))
            {
                var archiveDir = Path.Combine(_options.RootPath, "archive");
                Directory.CreateDirectory(archiveDir);
                var archiveAuditPath = Path.Combine(archiveDir, $"{specId}.audit.jsonl.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
                File.Move(auditPath, archiveAuditPath);
            }

            _logger.LogInformation("Deleted DecisionSpec {SpecId} v{Version}", specId, version);
        }

        return success;
    }

    public async Task<(DecisionSpecDocument Document, string ETag)?> RestoreAsync(string specId, string version, CancellationToken cancellationToken = default)
    {
        // Attempt restore from archive (default to Draft status)
        var success = await _fileStore.RestoreAsync(specId, version, "Draft", cancellationToken);
        
        if (!success)
        {
            return null;
        }

        // Restore audit log from archive
        var archiveDir = Path.Combine(_options.RootPath, "archive");
        if (Directory.Exists(archiveDir))
        {
            var auditFiles = Directory.GetFiles(archiveDir, $"{specId}.audit.jsonl.*").OrderByDescending(f => f).ToArray();
            if (auditFiles.Any())
            {
                var draftDir = Path.Combine(_options.RootPath, "draft");
                Directory.CreateDirectory(draftDir);
                var restoredAuditPath = Path.Combine(draftDir, $"{specId}.audit.jsonl");
                File.Move(auditFiles.First(), restoredAuditPath, overwrite: true);
            }
        }

        await _indexer.UpdateEntryAsync(specId, version, "Draft", cancellationToken);
        
        await AppendAuditEntryAsync(specId, new AuditEntry
        {
            SpecId = specId,
            Action = "Restored",
            Summary = $"Restored DecisionSpec {specId} v{version}",
            Actor = "System",
            Source = "API"
        }, cancellationToken);

        return await GetAsync(specId, version, cancellationToken);
    }

    public async Task<IEnumerable<DecisionSpecSummary>> ListAsync(string? status = null, string? owner = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var entries = await _indexer.QueryAsync(status, owner, searchTerm, cancellationToken);
        
        return entries.Select(e => new DecisionSpecSummary
        {
            SpecId = e.SpecId,
            Name = e.Name,
            Status = e.Status,
            Owner = e.Owner,
            Version = e.Version,
            UpdatedAt = e.UpdatedAt,
            TraitCount = e.TraitCount,
            HasUnverifiedDraft = e.HasUnverifiedDraft,
            ETag = e.ETag
        }).ToList();
    }

    public async Task<string?> GetFullDocumentJsonAsync(string specId, string? version = null, CancellationToken cancellationToken = default)
    {
        var result = await GetAsync(specId, version, cancellationToken);
        if (result == null)
        {
            return null;
        }

        return JsonSerializer.Serialize(result.Value.Document, GetSerializerOptions());
    }

    public async Task AppendAuditEntryAsync(string specId, AuditEntry entry, CancellationToken cancellationToken = default)
    {
        // Find which status directory the spec is in
        var auditPath = await GetAuditPathAsync(specId, cancellationToken);
        
        if (auditPath == null)
        {
            // Spec doesn't exist yet, use draft directory
            var draftDir = Path.Combine(_options.RootPath, "draft");
            Directory.CreateDirectory(draftDir);
            auditPath = Path.Combine(draftDir, $"{specId}.audit.jsonl");
        }

        var json = JsonSerializer.Serialize(entry);
        await File.AppendAllLinesAsync(auditPath, new[] { json }, cancellationToken);
    }

    public async Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(string specId, CancellationToken cancellationToken = default)
    {
        // Check all possible locations for audit file
        var auditPath = await GetAuditPathAsync(specId, cancellationToken);
        
        if (auditPath == null || !File.Exists(auditPath))
        {
            // Check archive directory
            var archiveDir = Path.Combine(_options.RootPath, "archive");
            if (Directory.Exists(archiveDir))
            {
                var archiveAuditFiles = Directory.GetFiles(archiveDir, $"{specId}.audit.jsonl*");
                if (archiveAuditFiles.Any())
                {
                    auditPath = archiveAuditFiles.OrderByDescending(f => f).First();
                }
            }
        }

        if (auditPath == null || !File.Exists(auditPath))
        {
            return Enumerable.Empty<AuditEntry>();
        }

        var lines = await File.ReadAllLinesAsync(auditPath, cancellationToken);
        var entries = new List<AuditEntry>();

        foreach (var line in lines)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse audit entry: {Line}", line);
            }
        }

        return entries.OrderByDescending(e => e.CreatedAt);
    }

    private async Task<string?> GetAuditPathAsync(string specId, CancellationToken cancellationToken)
    {
        // Try to find the spec in any status directory
        foreach (var status in new[] { "draft", "published", "inreview", "retired" })
        {
            var statusDir = Path.Combine(_options.RootPath, status);
            if (Directory.Exists(statusDir))
            {
                var specFiles = Directory.GetFiles(statusDir, $"{specId}.*.json");
                if (specFiles.Any())
                {
                    // Found the spec, return audit path in same directory
                    return Path.Combine(statusDir, $"{specId}.audit.jsonl");
                }
            }
        }

        return null;
    }
}
