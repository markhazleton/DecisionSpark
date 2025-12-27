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
    private readonly LegacyDecisionSpecAdapter _legacyAdapter;
    private readonly DecisionSpecsOptions _options;
    private readonly ILogger<DecisionSpecRepository> _logger;

    public DecisionSpecRepository(
        DecisionSpecFileStore fileStore,
        FileSearchIndexer indexer,
        LegacyDecisionSpecAdapter legacyAdapter,
        IOptions<DecisionSpecsOptions> options,
        ILogger<DecisionSpecRepository> logger)
    {
        _fileStore = fileStore;
        _indexer = indexer;
        _legacyAdapter = legacyAdapter;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(DecisionSpecDocument Document, string ETag)> CreateAsync(DecisionSpecDocument spec, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true });
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
                var doc = JsonSerializer.Deserialize<DecisionSpecDocument>(content);
                if (doc != null)
                {
                    return (doc, etag);
                }
            }
        }

        // Try legacy location if configured
        if (!string.IsNullOrWhiteSpace(_options.LegacyConfigPath) && Directory.Exists(_options.LegacyConfigPath))
        {
            var legacyFiles = Directory.GetFiles(_options.LegacyConfigPath, $"{specId}*.active.json", SearchOption.TopDirectoryOnly);
            
            if (legacyFiles.Length > 0)
            {
                var filePath = legacyFiles[0]; // Take first match
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                var fileName = Path.GetFileName(filePath);
                
                var converted = _legacyAdapter.ConvertLegacySpec(content, fileName);
                if (converted != null)
                {
                    var etag = ComputeETag(content);
                    _logger.LogInformation("Loaded legacy spec {SpecId} from {FileName}", specId, fileName);
                    return (converted, etag);
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
        var json = JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true });
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

        var success = await _fileStore.SoftDeleteAsync(specId, version, current.Value.Document.Status, cancellationToken);
        
        if (success)
        {
            await _indexer.RemoveEntryAsync(specId, cancellationToken);
            
            await AppendAuditEntryAsync(specId, new AuditEntry
            {
                SpecId = specId,
                Action = "Deleted",
                Summary = $"Soft-deleted DecisionSpec {specId} v{version}",
                Actor = "System",
                Source = "API"
            }, cancellationToken);

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
            QuestionCount = e.QuestionCount,
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

        return JsonSerializer.Serialize(result.Value.Document, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task AppendAuditEntryAsync(string specId, AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var auditPath = Path.Combine(Path.GetDirectoryName(_fileStore.ListFiles("draft").FirstOrDefault() ?? "") ?? "", $"{specId}.audit.jsonl");
        var auditDir = Path.GetDirectoryName(auditPath);
        
        if (!string.IsNullOrEmpty(auditDir) && !Directory.Exists(auditDir))
        {
            Directory.CreateDirectory(auditDir);
        }

        var json = JsonSerializer.Serialize(entry);
        await File.AppendAllLinesAsync(auditPath, new[] { json }, cancellationToken);
    }

    public async Task<IEnumerable<AuditEntry>> GetAuditHistoryAsync(string specId, CancellationToken cancellationToken = default)
    {
        var auditPath = Path.Combine(Path.GetDirectoryName(_fileStore.ListFiles("draft").FirstOrDefault() ?? "") ?? "", $"{specId}.audit.jsonl");
        
        if (!File.Exists(auditPath))
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
}
