using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;

namespace DecisionSpark.Services;

/// <summary>
/// Processes and normalizes user selections from structured options or free text.
/// Implements FR-024a (structured priority), FR-024b (empty selection handling),
/// and negative option enforcement (FR-006).
/// </summary>
public interface IUserSelectionService
{
    /// <summary>
    /// Normalizes the user's input, prioritizing structured selections over free text.
    /// </summary>
    /// <param name="request">The next request from the user</param>
    /// <param name="questionType">The type of question (text/single-select/multi-select)</param>
    /// <param name="availableOptions">Available options with their metadata</param>
    /// <returns>Normalized selection with canonical values</returns>
    UserSelection NormalizeSelection(
        NextRequest request,
        string questionType,
        List<QuestionOptionDto>? availableOptions = null);
}

public class UserSelectionService : IUserSelectionService
{
    private readonly ILogger<UserSelectionService> _logger;

    public UserSelectionService(ILogger<UserSelectionService> logger)
    {
        _logger = logger;
    }

    public UserSelection NormalizeSelection(
        NextRequest request,
        string questionType,
        List<QuestionOptionDto>? availableOptions = null)
    {
        var selection = new UserSelection
        {
            QuestionType = questionType
        };

        // FR-024a: Structured selections override free text
        if (request.SelectedOptionIds != null && request.SelectedOptionIds.Length > 0)
        {
            _logger.LogInformation(
                "[UserSelectionService] Processing {Count} structured option IDs",
                request.SelectedOptionIds.Length);

            // Enforce max 7 options (already done in binder, but double-check)
            var optionIds = request.SelectedOptionIds.Take(7).ToArray();

            // Handle negative options (FR-006)
            var negativeOption = DetectNegativeOption(optionIds, availableOptions);
            if (negativeOption != null)
            {
                var clearedCount = optionIds.Length - 1;
                _logger.LogInformation(
                    "[UserSelectionService] Negative option '{NegativeId}' detected, clearing {ClearedCount} other selection(s)",
                    negativeOption.Id, clearedCount);

                // Log telemetry for negative option override (FR-042)
                _logger.LogInformation(
                    "[Telemetry] NegativeOptionOverride: OptionId={NegativeId}, ClearedCount={ClearedCount}, QuestionType={QuestionType}",
                    negativeOption.Id, clearedCount, questionType);

                selection.SelectedOptionIds = new[] { negativeOption.Id };
                selection.SelectedValues = new[] { negativeOption.Value };
                selection.ValidationStatus = "Passed";
                return selection;
            }

            // Map option IDs to canonical values
            selection.SelectedOptionIds = optionIds;
            selection.SelectedValues = MapOptionsToValues(optionIds, availableOptions);

            // Log when free text is ignored
            if (!string.IsNullOrWhiteSpace(request.UserInput))
            {
                _logger.LogInformation(
                    "[UserSelectionService] Ignoring free text '{UserInput}' because structured selections are present",
                    request.UserInput);
            }

            selection.ValidationStatus = "Passed";
            return selection;
        }

        // No structured selections - use free text
        if (!string.IsNullOrWhiteSpace(request.UserInput))
        {
            _logger.LogInformation(
                "[UserSelectionService] Using free text input: '{UserInput}'",
                request.UserInput);

            selection.SubmittedText = request.UserInput;
            selection.ValidationStatus = "Pending"; // Will be validated by parser
            return selection;
        }

        // FR-024b: Empty submission handling
        _logger.LogWarning("[UserSelectionService] Empty submission received");
        selection.ValidationStatus = "Failed";
        selection.ErrorReason = "No input provided";

        return selection;
    }

    private QuestionOptionDto? DetectNegativeOption(
        string[] selectedIds,
        List<QuestionOptionDto>? availableOptions)
    {
        if (availableOptions == null || availableOptions.Count == 0)
        {
            return null;
        }

        // Check if any selected option is marked as negative
        foreach (var id in selectedIds)
        {
            var option = availableOptions.FirstOrDefault(o => o.Id == id);
            if (option != null && option.IsNegative)
            {
                return option;
            }
        }

        return null;
    }

    private string[] MapOptionsToValues(
        string[] optionIds,
        List<QuestionOptionDto>? availableOptions)
    {
        if (availableOptions == null || availableOptions.Count == 0)
        {
            // No mapping available, return IDs as values
            _logger.LogWarning(
                "[UserSelectionService] No available options for mapping, using IDs as values");
            return optionIds;
        }

        var values = new List<string>();
        foreach (var id in optionIds)
        {
            var option = availableOptions.FirstOrDefault(o => o.Id == id);
            if (option != null)
            {
                values.Add(option.Value);
                _logger.LogDebug(
                    "[UserSelectionService] Mapped option ID '{Id}' to value '{Value}'",
                    id, option.Value);
            }
            else
            {
                _logger.LogWarning(
                    "[UserSelectionService] Option ID '{Id}' not found in available options, using ID as value",
                    id);
                values.Add(id);
            }
        }

        return values.ToArray();
    }
}

/// <summary>
/// Represents the normalized user selection after processing.
/// </summary>
public class UserSelection
{
    public string QuestionType { get; set; } = string.Empty;
    public string[]? SelectedOptionIds { get; set; }
    public string[]? SelectedValues { get; set; }
    public string? SubmittedText { get; set; }
    public string ValidationStatus { get; set; } = "Pending"; // Pending, Passed, Failed
    public string? ErrorReason { get; set; }
}
