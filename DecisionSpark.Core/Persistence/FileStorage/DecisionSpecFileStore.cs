using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DecisionSpark.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DecisionSpark.Core.Persistence.FileStorage;

/// <summary>
/// Handles atomic file writes, locking, archival, and ETag generation for DecisionSpec JSON files.
/// </summary>
public class DecisionSpecFileStore
{
    private readonly DecisionSpecsOptions _options;
    private readonly ILogger<DecisionSpecFileStore> _logger;
    private static readonly SemaphoreSlim _writeLock = new(1, 1);

    public DecisionSpecFileStore(IOptions<DecisionSpecsOptions> options, ILogger<DecisionSpecFileStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Writes a DecisionSpec to disk atomically using temp-file swap.
    /// </summary>
    public async Task<string> WriteAsync(string specId, string version, string status, string jsonContent, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var targetDir = GetStatusDirectory(status);
            Directory.CreateDirectory(targetDir);

            var fileName = $"{specId}.{version}.{status}.json";
            var targetPath = Path.Combine(targetDir, fileName);
            var tempPath = Path.Combine(targetDir, $"{fileName}.tmp");

            // Write to temp file first
            await File.WriteAllTextAsync(tempPath, jsonContent, Encoding.UTF8, cancellationToken);

            // Atomic replace
            File.Move(tempPath, targetPath, overwrite: true);

            // Generate ETag
            var etag = GenerateETag(jsonContent);

            _logger.LogInformation("Wrote DecisionSpec {SpecId} v{Version} with ETag {ETag}", specId, version, etag);

            return etag;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads a DecisionSpec file and returns its content and ETag.
    /// </summary>
    public async Task<(string Content, string ETag)?> ReadAsync(string specId, string version, string status, CancellationToken cancellationToken = default)
    {
        var targetDir = GetStatusDirectory(status);
        var fileName = $"{specId}.{version}.{status}.json";
        var filePath = Path.Combine(targetDir, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var etag = GenerateETag(content);

        return (content, etag);
    }

    /// <summary>
    /// Soft-deletes a spec by moving it to the archive folder.
    /// </summary>
    public async Task<bool> SoftDeleteAsync(string specId, string version, string status, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var sourceDir = GetStatusDirectory(status);
            var fileName = $"{specId}.{version}.{status}.json";
            var sourcePath = Path.Combine(sourceDir, fileName);

            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var archiveDir = Path.Combine(_options.RootPath, "archive");
            Directory.CreateDirectory(archiveDir);

            var archivePath = Path.Combine(archiveDir, $"{fileName}.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}");
            File.Move(sourcePath, archivePath);

            _logger.LogInformation("Soft-deleted DecisionSpec {SpecId} v{Version} to {ArchivePath}", specId, version, archivePath);

            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Restores a soft-deleted spec from the archive folder.
    /// </summary>
    public async Task<bool> RestoreAsync(string specId, string version, string status, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var archiveDir = Path.Combine(_options.RootPath, "archive");
            
            if (!Directory.Exists(archiveDir))
            {
                return false;
            }

            var pattern = $"{specId}.{version}.{status}.json.*";
            var archiveFiles = Directory.GetFiles(archiveDir, pattern).OrderByDescending(f => f).ToList();

            if (!archiveFiles.Any())
            {
                return false;
            }

            var latestArchive = archiveFiles.First();
            var targetDir = GetStatusDirectory(status);
            Directory.CreateDirectory(targetDir);

            var fileName = $"{specId}.{version}.{status}.json";
            var targetPath = Path.Combine(targetDir, fileName);

            File.Move(latestArchive, targetPath, overwrite: true);

            _logger.LogInformation("Restored DecisionSpec {SpecId} v{Version} from archive", specId, version);

            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Lists all spec files in a given status directory.
    /// </summary>
    public IEnumerable<string> ListFiles(string status)
    {
        var targetDir = GetStatusDirectory(status);
        if (!Directory.Exists(targetDir))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(targetDir, "*.json", SearchOption.TopDirectoryOnly);
    }

    /// <summary>
    /// Generates a SHA-256 based ETag for optimistic concurrency.
    /// </summary>
    public string GenerateETag(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    private string GetStatusDirectory(string status)
    {
        var folderName = status.ToLowerInvariant() switch
        {
            "draft" => "draft",
            "inreview" => "inreview",
            "published" => "published",
            "retired" => "retired",
            _ => "draft"
        };

        return Path.Combine(_options.RootPath, folderName);
    }
}
