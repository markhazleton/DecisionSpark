using System.Text.Json;
using DecisionSpark.Core.Models.Configuration;
using DecisionSpark.Core.Models.Spec;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionSpark.Core.Persistence.FileStorage;

/// <summary>
/// Manages lifecycle metadata storage in sidecar files separate from core DecisionSpec JSON.
/// Keeps runtime schema pristine while supporting CRUD management needs.
/// </summary>
public class DecisionSpecMetadataStore
{
    private readonly DecisionSpecsOptions _options;
    private readonly ILogger<DecisionSpecMetadataStore> _logger;
    private static readonly SemaphoreSlim _metadataLock = new(1, 1);

    public DecisionSpecMetadataStore(
        IOptions<DecisionSpecsOptions> options,
        ILogger<DecisionSpecMetadataStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Saves metadata to sidecar file.
    /// </summary>
    public async Task SaveMetadataAsync(
        string specId,
        string version,
        DecisionSpecMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            var statusDir = GetStatusDirectory(metadata.Status);
            Directory.CreateDirectory(statusDir);

            var fileName = $"{specId}.{version}.metadata.json";
            var filePath = Path.Combine(statusDir, fileName);

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Saved metadata for {SpecId} v{Version} with status {Status}",
                specId, version, metadata.Status);
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    /// <summary>
    /// Loads metadata from sidecar file.
    /// </summary>
    public async Task<DecisionSpecMetadata?> LoadMetadataAsync(
        string specId,
        string version,
        string status,
        CancellationToken cancellationToken = default)
    {
        var statusDir = GetStatusDirectory(status);
        var fileName = $"{specId}.{version}.metadata.json";
        var filePath = Path.Combine(statusDir, fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Metadata file not found for {SpecId} v{Version} with status {Status}",
                specId, version, status);
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<DecisionSpecMetadata>(json);

        return metadata;
    }

    /// <summary>
    /// Searches for metadata across all status folders.
    /// </summary>
    public async Task<DecisionSpecMetadata?> FindMetadataAsync(
        string specId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var statuses = new[] { "Draft", "InReview", "Published", "Retired" };

        foreach (var status in statuses)
        {
            var statusDir = GetStatusDirectory(status);
            if (!Directory.Exists(statusDir))
            {
                continue;
            }

            // If version specified, look for exact match
            if (!string.IsNullOrEmpty(version))
            {
                var metadata = await LoadMetadataAsync(specId, version, status, cancellationToken);
                if (metadata != null)
                {
                    return metadata;
                }
            }
            else
            {
                // Find latest version for this specId in this status
                var pattern = $"{specId}.*.metadata.json";
                var files = Directory.GetFiles(statusDir, pattern, SearchOption.TopDirectoryOnly);

                if (files.Length > 0)
                {
                    // Sort by file name (version) descending
                    var latestFile = files.OrderByDescending(f => f).First();
                    var json = await File.ReadAllTextAsync(latestFile, cancellationToken);
                    var metadata = JsonSerializer.Deserialize<DecisionSpecMetadata>(json);
                    if (metadata != null)
                    {
                        return metadata;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Moves metadata file when status changes.
    /// </summary>
    public async Task MoveMetadataAsync(
        string specId,
        string version,
        string oldStatus,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            var oldDir = GetStatusDirectory(oldStatus);
            var newDir = GetStatusDirectory(newStatus);
            Directory.CreateDirectory(newDir);

            var fileName = $"{specId}.{version}.metadata.json";
            var oldPath = Path.Combine(oldDir, fileName);
            var newPath = Path.Combine(newDir, fileName);

            if (File.Exists(oldPath))
            {
                // Load, update status, save to new location
                var json = await File.ReadAllTextAsync(oldPath, cancellationToken);
                var metadata = JsonSerializer.Deserialize<DecisionSpecMetadata>(json);
                
                if (metadata != null)
                {
                    metadata.Status = newStatus;
                    metadata.UpdatedAt = DateTimeOffset.UtcNow;
                    
                    await SaveMetadataAsync(specId, version, metadata, cancellationToken);
                    File.Delete(oldPath);
                }

                _logger.LogInformation("Moved metadata for {SpecId} v{Version} from {OldStatus} to {NewStatus}",
                    specId, version, oldStatus, newStatus);
            }
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    /// <summary>
    /// Deletes metadata file (for soft-delete archival).
    /// </summary>
    public async Task DeleteMetadataAsync(
        string specId,
        string version,
        string status,
        CancellationToken cancellationToken = default)
    {
        await _metadataLock.WaitAsync(cancellationToken);
        try
        {
            var statusDir = GetStatusDirectory(status);
            var fileName = $"{specId}.{version}.metadata.json";
            var filePath = Path.Combine(statusDir, fileName);

            if (File.Exists(filePath))
            {
                // Move to archive instead of deleting
                var archiveDir = Path.Combine(_options.RootPath, "archive");
                Directory.CreateDirectory(archiveDir);

                var archivePath = Path.Combine(archiveDir, 
                    $"{fileName}.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
                File.Move(filePath, archivePath);

                _logger.LogInformation("Archived metadata for {SpecId} v{Version}",
                    specId, version);
            }
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private string GetStatusDirectory(string status)
    {
        var statusFolder = status.ToLowerInvariant() switch
        {
            "draft" => "draft",
            "inreview" => "inreview",
            "published" => "published",
            "retired" => "retired",
            _ => "draft"
        };

        return Path.Combine(_options.RootPath, statusFolder);
    }
}
