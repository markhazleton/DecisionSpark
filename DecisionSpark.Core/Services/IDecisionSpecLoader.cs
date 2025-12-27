using DecisionSpark.Core.Models.Spec;
using System.Text.Json;

namespace DecisionSpark.Core.Services;

public interface IDecisionSpecLoader
{
    Task<DecisionSpec> LoadActiveSpecAsync(string specId);
    void ValidateSpec(DecisionSpec spec);
}

public class FileSystemDecisionSpecLoader : IDecisionSpecLoader
{
    private readonly ILogger<FileSystemDecisionSpecLoader> _logger;
    private readonly string _configBasePath;
    private DecisionSpec? _cachedSpec;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly IWebHostEnvironment _environment;

    public FileSystemDecisionSpecLoader(
        ILogger<FileSystemDecisionSpecLoader> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;

        // Get the config path from configuration or use default
        var configPath = configuration["DecisionEngine:ConfigPath"] ?? "Config/DecisionSpecs";

        // Try multiple paths to handle different hosting scenarios
        var candidatePaths = new List<string>();

        // 1. Relative to ContentRootPath (source directory in IIS Express)
        if (!Path.IsPathRooted(configPath))
        {
            candidatePaths.Add(Path.Combine(environment.ContentRootPath, configPath));

            // 2. Relative to the application base directory (bin output)
            var baseDir = AppContext.BaseDirectory;
            candidatePaths.Add(Path.Combine(baseDir, configPath));
        }
        else
        {
            candidatePaths.Add(configPath);
        }

        // Find the first path that exists
        _configBasePath = candidatePaths.FirstOrDefault(Directory.Exists) ?? candidatePaths[0];

        _logger.LogInformation("DecisionSpec base path: {Path} (Exists: {Exists})",
            _configBasePath, Directory.Exists(_configBasePath));

        if (!Directory.Exists(_configBasePath))
        {
            _logger.LogWarning("Config directory not found. Searched paths: {Paths}",
                string.Join(", ", candidatePaths));
        }
    }

    public async Task<DecisionSpec> LoadActiveSpecAsync(string specId)
    {
        if (_cachedSpec != null && _cachedSpec.SpecId == specId)
        {
            _logger.LogDebug("Returning cached spec {SpecId}", specId);
            return _cachedSpec;
        }

        await _loadLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cachedSpec != null && _cachedSpec.SpecId == specId)
            {
                return _cachedSpec;
            }

            // Ensure directory exists
            if (!Directory.Exists(_configBasePath))
            {
                throw new DirectoryNotFoundException($"Config directory not found: {_configBasePath}");
            }

            var searchPattern = $"{specId}.*.active.json";
            _logger.LogDebug("Searching for spec files matching: {Pattern} in {Path}", searchPattern, _configBasePath);

            var files = Directory.GetFiles(_configBasePath, searchPattern);

            if (files.Length == 0)
            {
                throw new FileNotFoundException($"No active spec found for {specId} in {_configBasePath}");
            }

            if (files.Length > 1)
            {
                _logger.LogWarning("Multiple active specs found for {SpecId}, using first", specId);
            }

            var filePath = files[0];
            _logger.LogInformation("Loading spec from {FilePath}", filePath);

            var json = await File.ReadAllTextAsync(filePath);

            // Configure JSON options to handle snake_case property names
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var spec = JsonSerializer.Deserialize<DecisionSpec>(json, options);

            if (spec == null)
            {
                throw new InvalidOperationException($"Failed to deserialize spec from {filePath}");
            }

            ValidateSpec(spec);
            _cachedSpec = spec;

            _logger.LogInformation("Loaded and validated spec {SpecId} version {Version}", spec.SpecId, spec.Version);
            return spec;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading spec {SpecId} from {Path}", specId, _configBasePath);
            throw;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public void ValidateSpec(DecisionSpec spec)
    {
        if (string.IsNullOrEmpty(spec.SpecId))
            throw new InvalidOperationException("Spec must have a SpecId");

        if (string.IsNullOrEmpty(spec.Version))
            throw new InvalidOperationException("Spec must have a Version");

        if (spec.Traits.Count == 0)
            throw new InvalidOperationException("Spec must have at least one trait");

        if (spec.Outcomes.Count == 0)
            throw new InvalidOperationException("Spec must have at least one outcome");

        var traitKeys = new HashSet<string>();
        foreach (var trait in spec.Traits)
        {
            if (!traitKeys.Add(trait.Key))
                throw new InvalidOperationException($"Duplicate trait key: {trait.Key}");
        }

        _logger.LogDebug("Spec validation passed");
    }
}
