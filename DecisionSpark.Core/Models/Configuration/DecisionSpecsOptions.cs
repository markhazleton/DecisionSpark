namespace DecisionSpark.Core.Models.Configuration;

/// <summary>
/// Configuration options for DecisionSpec file storage and management.
/// </summary>
public class DecisionSpecsOptions
{
    public const string SectionName = "DecisionSpecs";

    /// <summary>
    /// Root path where DecisionSpec JSON files are stored (with status subfolders).
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Legacy path where old format DecisionSpec files are stored (optional, for backward compatibility).
    /// If specified, the system will scan this location for .active.json files and convert them.
    /// </summary>
    public string? LegacyConfigPath { get; set; }

    /// <summary>
    /// Number of days to retain soft-deleted specs before permanent removal.
    /// </summary>
    public int SoftDeleteRetentionDays { get; set; } = 30;

    /// <summary>
    /// Filename for the search index cache (stored in each status folder).
    /// </summary>
    public string IndexFileName { get; set; } = "DecisionSpecIndex.json";
}
