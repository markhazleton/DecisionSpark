using System.Text.Json;
using DecisionSpark.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionSpark.Core.Persistence.FileStorage;

/// <summary>
/// Summary information for a DecisionSpec in the search index.
/// </summary>
public class DecisionSpecIndexEntry
{
    public string SpecId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int TraitCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public bool HasUnverifiedDraft { get; set; }
    public string ETag { get; set; } = string.Empty;
}

/// <summary>
/// Maintains DecisionSpecIndex.json entries for fast filtering and search.
/// </summary>
public class FileSearchIndexer
{
    private readonly DecisionSpecsOptions _options;
    private readonly ILogger<FileSearchIndexer> _logger;
    private readonly DecisionSpecFileStore _fileStore;
    private static readonly SemaphoreSlim _indexLock = new(1, 1);

    public FileSearchIndexer(
        IOptions<DecisionSpecsOptions> options,
        ILogger<FileSearchIndexer> logger,
        DecisionSpecFileStore fileStore)
    {
        _options = options.Value;
        _logger = logger;
        _fileStore = fileStore;
    }

    /// <summary>
    /// Adds or updates an index entry for a spec.
    /// </summary>
    public async Task UpdateEntryAsync(string specId, string version, string status, CancellationToken cancellationToken = default)
    {
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            var result = await _fileStore.ReadAsync(specId, version, status, cancellationToken);
            if (result == null)
            {
                _logger.LogWarning("Cannot update index for non-existent spec {SpecId}", specId);
                return;
            }

            var (content, etag) = result.Value;
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var entry = new DecisionSpecIndexEntry
            {
                SpecId = specId,
                Version = version,
                Status = status,
                Name = root.GetProperty("metadata").TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? specId : specId,
                Owner = root.GetProperty("metadata").TryGetProperty("owner", out var ownerProp) ? ownerProp.GetString() ?? "Unknown" : "Unknown",
                TraitCount = root.TryGetProperty("traits", out var traitsProp) ? traitsProp.GetArrayLength() : 0,
                UpdatedAt = DateTimeOffset.UtcNow,
                HasUnverifiedDraft = root.TryGetProperty("metadata", out var metaProp) && 
                                     metaProp.TryGetProperty("unverified", out var unverifiedProp) && 
                                     unverifiedProp.GetBoolean(),
                ETag = etag
            };

            var index = await LoadIndexAsync(cancellationToken);
            index[specId] = entry;
            await SaveIndexAsync(index, cancellationToken);

            _logger.LogDebug("Updated index entry for {SpecId}", specId);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Removes an entry from the index.
    /// </summary>
    public async Task RemoveEntryAsync(string specId, CancellationToken cancellationToken = default)
    {
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            var index = await LoadIndexAsync(cancellationToken);
            index.Remove(specId);
            await SaveIndexAsync(index, cancellationToken);

            _logger.LogDebug("Removed index entry for {SpecId}", specId);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Queries the index with optional filters.
    /// </summary>
    public async Task<IEnumerable<DecisionSpecIndexEntry>> QueryAsync(
        string? status = null,
        string? owner = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var index = await LoadIndexAsync(cancellationToken);
        var results = index.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            results = results.Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(owner))
        {
            results = results.Where(e => e.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            results = results.Where(e =>
                e.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                e.SpecId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return results.OrderByDescending(e => e.UpdatedAt).ToList();
    }

    /// <summary>
    /// Rebuilds the entire index from file system.
    /// </summary>
    public async Task RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Rebuilding DecisionSpec index");

            var newIndex = new Dictionary<string, DecisionSpecIndexEntry>();
            var statuses = new[] { "draft", "inreview", "published", "retired" };

            // Scan new format files
            foreach (var status in statuses)
            {
                var files = _fileStore.ListFiles(status);
                foreach (var filePath in files)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var parts = fileName.Split('.');
                        if (parts.Length >= 3)
                        {
                            var specId = string.Join(".", parts.Take(parts.Length - 2));
                            var version = parts[^2];
                            var fileStatus = parts[^1];

                            var result = await _fileStore.ReadAsync(specId, version, fileStatus, cancellationToken);
                            if (result != null)
                            {
                                var (content, etag) = result.Value;
                                var doc = JsonDocument.Parse(content);
                                var root = doc.RootElement;

                                var hasMetadata = root.TryGetProperty("metadata", out var metadataElement);
                                var entry = new DecisionSpecIndexEntry
                                {
                                    SpecId = specId,
                                    Version = version,
                                    Status = fileStatus,
                                    Name = hasMetadata && metadataElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? specId : specId,
                                    Owner = hasMetadata && metadataElement.TryGetProperty("owner", out var ownerProp) ? ownerProp.GetString() ?? "Unknown" : "Unknown",
                                    TraitCount = root.TryGetProperty("traits", out var traitsProp) ? traitsProp.GetArrayLength() : 0,
                                    UpdatedAt = File.GetLastWriteTimeUtc(filePath),
                                    HasUnverifiedDraft = hasMetadata &&
                                                         metadataElement.TryGetProperty("unverified", out var unverifiedProp) &&
                                                         unverifiedProp.GetBoolean(),
                                    ETag = etag
                                };

                                newIndex[specId] = entry;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to index file {FilePath}", filePath);
                    }
                }
            }

            await SaveIndexAsync(newIndex, cancellationToken);
            _logger.LogInformation("Index rebuilt with {Count} entries", newIndex.Count);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task<Dictionary<string, DecisionSpecIndexEntry>> LoadIndexAsync(CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(_options.RootPath, _options.IndexFileName);
        
        if (!File.Exists(indexPath))
        {
            return new Dictionary<string, DecisionSpecIndexEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(indexPath, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<DecisionSpecIndexEntry>>(json) ?? new List<DecisionSpecIndexEntry>();
            return entries.ToDictionary(e => e.SpecId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load index, returning empty index");
            return new Dictionary<string, DecisionSpecIndexEntry>();
        }
    }

    private async Task SaveIndexAsync(Dictionary<string, DecisionSpecIndexEntry> index, CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(_options.RootPath, _options.IndexFileName);
        var entries = index.Values.OrderBy(e => e.SpecId).ToList();
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(indexPath, json, cancellationToken);
    }

    private static string ComputeETag(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }
}
