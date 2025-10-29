using DecisionSpark.Models.Spec;
using System.Text.Json;

namespace DecisionSpark.Services;

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

    public FileSystemDecisionSpecLoader(ILogger<FileSystemDecisionSpecLoader> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configBasePath = configuration["DecisionEngine:ConfigPath"] ?? "Config/DecisionSpecs";
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

   var pattern = Path.Combine(_configBasePath, $"{specId}.*.active.json");
        var files = Directory.GetFiles(_configBasePath, $"{specId}.*.active.json");
      
            if (files.Length == 0)
     {
   throw new FileNotFoundException($"No active spec found for {specId}");
            }

     if (files.Length > 1)
    {
                _logger.LogWarning("Multiple active specs found for {SpecId}, using first", specId);
         }

      var filePath = files[0];
  _logger.LogInformation("Loading spec from {FilePath}", filePath);

            var json = await File.ReadAllTextAsync(filePath);
            var spec = JsonSerializer.Deserialize<DecisionSpec>(json, new JsonSerializerOptions
            {
        PropertyNameCaseInsensitive = true
            });

   if (spec == null)
       {
        throw new InvalidOperationException($"Failed to deserialize spec from {filePath}");
            }

      ValidateSpec(spec);
    _cachedSpec = spec;
            
  _logger.LogInformation("Loaded and validated spec {SpecId} version {Version}", spec.SpecId, spec.Version);
         return spec;
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
