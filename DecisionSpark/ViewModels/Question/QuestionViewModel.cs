using DecisionSpark.Core.Models.Api;

namespace DecisionSpark.ViewModels.Question;

/// <summary>
/// Razor-friendly projection of QuestionDto for rendering specific controls.
/// </summary>
public class QuestionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public QuestionInputType InputType { get; set; }
    public List<OptionRenderState> Options { get; set; } = new();
    public bool ShowCustomInput { get; set; }
    public int? RetryAttempt { get; set; }
    public QuestionMetadataDto? Metadata { get; set; }

    public static QuestionViewModel FromDto(QuestionDto dto)
    {
        var inputType = dto.Type.ToLowerInvariant() switch
        {
            "single-select" => QuestionInputType.SingleSelect,
            "multi-select" => QuestionInputType.MultiSelect,
            _ => QuestionInputType.Text
        };

        return new QuestionViewModel
        {
            Id = dto.Id,
            Prompt = dto.Text,
            InputType = inputType,
            Options = dto.Options.Take(7).Select(o => new OptionRenderState
            {
                Id = o.Id,
                Label = o.Label,
                Value = o.Value,
                IsNegative = o.IsNegative,
                IsDefault = o.IsDefault,
                IsSelected = o.IsDefault
            }).ToList(),
            ShowCustomInput = dto.AllowFreeText,
            RetryAttempt = dto.RetryAttempt,
            Metadata = dto.Metadata
        };
    }
}

/// <summary>
/// Tracks checkbox/radio selection plus negative-option enforcement on the client.
/// </summary>
public class OptionRenderState
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsNegative { get; set; }
    public bool IsDefault { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>
    /// Gets the display label, truncated to 60 characters if needed.
    /// </summary>
    public string DisplayLabel => Label.Length > 60 ? Label.Substring(0, 57) + "..." : Label;

    /// <summary>
    /// Returns true if the label was truncated.
    /// </summary>
    public bool IsTruncated => Label.Length > 60;
}

public enum QuestionInputType
{
    Text,
    SingleSelect,
    MultiSelect
}
