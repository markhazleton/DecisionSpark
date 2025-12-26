using System.Text;
using System.Text.RegularExpressions;

namespace DecisionSpark.Services;

/// <summary>
/// Generates stable, deterministic option IDs from labels.
/// Implements FR-034a: lowercase, hyphens, alphanumeric only.
/// </summary>
public interface IOptionIdGenerator
{
    /// <summary>
    /// Generates a stable slug ID from a label.
    /// </summary>
    /// <param name="label">The option label text</param>
    /// <returns>A lowercase, hyphen-separated, alphanumeric slug</returns>
    string GenerateId(string label);
}

public class OptionIdGenerator : IOptionIdGenerator
{
    private readonly ILogger<OptionIdGenerator> _logger;

    public OptionIdGenerator(ILogger<OptionIdGenerator> logger)
    {
        _logger = logger;
    }

    public string GenerateId(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            _logger.LogWarning("[OptionIdGenerator] Empty label provided, returning 'unknown'");
            return "unknown";
        }

        // Normalize to lowercase
        var slug = label.ToLowerInvariant();

        // Replace spaces and underscores with hyphens
        slug = Regex.Replace(slug, @"[\s_]+", "-");

        // Remove all non-alphanumeric characters except hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Remove leading/trailing hyphens
        slug = slug.Trim('-');

        // Collapse multiple consecutive hyphens into one
        slug = Regex.Replace(slug, @"\-+", "-");

        // Ensure we have a valid ID
        if (string.IsNullOrWhiteSpace(slug))
        {
            _logger.LogWarning("[OptionIdGenerator] Label '{Label}' produced empty slug, returning 'option'", label);
            return "option";
        }

        _logger.LogDebug("[OptionIdGenerator] Generated ID '{Slug}' from label '{Label}'", slug, label);
        return slug;
    }
}
