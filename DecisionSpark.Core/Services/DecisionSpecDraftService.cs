using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services.Validation;
using FluentValidation;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DecisionSpark.Core.Services;

/// <summary>
/// Service for orchestrating LLM-assisted DecisionSpec draft generation.
/// Handles prompt templates, OpenAI integration, draft caching, and validation.
/// </summary>
public class DecisionSpecDraftService
{
    private readonly IOpenAIService _openAIService;
    private readonly IDecisionSpecRepository _repository;
    private readonly IValidator<DecisionSpecDocument> _validator;
    private readonly ILogger<DecisionSpecDraftService> _logger;
    private readonly string _draftsPath;

    public DecisionSpecDraftService(
        IOpenAIService openAIService,
        IDecisionSpecRepository repository,
        IValidator<DecisionSpecDocument> validator,
        ILogger<DecisionSpecDraftService> logger,
        string draftsPath)
    {
        _openAIService = openAIService ?? throw new ArgumentNullException(nameof(openAIService));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _draftsPath = draftsPath ?? throw new ArgumentNullException(nameof(draftsPath));

        // Ensure drafts directory exists
        Directory.CreateDirectory(_draftsPath);
    }

    /// <summary>
    /// Generates a DecisionSpec draft from a natural language instruction using LLM.
    /// </summary>
    /// <param name="instruction">Natural language description of the desired DecisionSpec (max 2000 chars)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated draft with unverified flag set</returns>
    public async Task<(DecisionSpecDocument Draft, string DraftId)> GenerateDraftAsync(
        string instruction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new ArgumentException("Instruction cannot be empty", nameof(instruction));
        }

        if (instruction.Length > 2000)
        {
            throw new ArgumentException("Instruction must be 2000 characters or less", nameof(instruction));
        }

        var draftId = $"DRAFT_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

        _logger.LogInformation("Generating DecisionSpec draft {DraftId} from instruction (length: {Length})", 
            draftId, instruction.Length);

        try
        {
            // Build prompt for LLM
            var prompt = BuildDraftPrompt(instruction);

            // Call OpenAI service to generate the draft
            var request = new OpenAICompletionRequest
            {
                SystemPrompt = "You are an expert at creating DecisionSpec documents for decision routing systems. Generate only valid JSON with snake_case properties.",
                UserPrompt = prompt,
                MaxTokens = 2000,
                Temperature = 0.7f
            };

            var response = await _openAIService.GetCompletionAsync(request);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
            {
                throw new InvalidOperationException($"LLM service failed: {response.ErrorMessage ?? "Empty response"}");
            }

            _logger.LogInformation("Received LLM response for draft {DraftId} (length: {Length})", 
                draftId, response.Content.Length);

            // Parse the response into a DecisionSpec document
            var draft = ParseLlmResponse(response.Content, instruction, draftId);

            // Mark as unverified
            draft.Metadata.Unverified = true;
            draft.Status = "Draft";
            draft.Metadata.CreatedAt = DateTimeOffset.UtcNow;
            draft.Metadata.UpdatedAt = DateTimeOffset.UtcNow;
            draft.Metadata.CreatedBy = "LLM";
            draft.Metadata.UpdatedBy = "LLM";

            // Validate the draft (log errors but don't block)
            var validationResult = await _validator.ValidateAsync(draft, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Generated draft {DraftId} has validation errors: {Errors}",
                    draftId,
                    string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
            }

            // Cache the draft
            await CacheDraftAsync(draftId, draft, cancellationToken);

            _logger.LogInformation("Successfully generated draft {DraftId} with {TraitCount} traits and {OutcomeCount} outcomes",
                draftId, draft.Traits.Count, draft.Outcomes.Count);

            return (draft, draftId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating draft {DraftId} from instruction", draftId);
            throw new InvalidOperationException($"Failed to generate draft: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieves a previously generated draft from cache.
    /// </summary>
    public async Task<DecisionSpecDocument?> GetDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        var draftFile = Path.Combine(_draftsPath, $"{draftId}.json");

        if (!File.Exists(draftFile))
        {
            _logger.LogWarning("Draft {DraftId} not found in cache", draftId);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(draftFile, cancellationToken);
            var draft = JsonSerializer.Deserialize<DecisionSpecDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Retrieved draft {DraftId} from cache", draftId);
            return draft;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading draft {DraftId} from cache", draftId);
            return null;
        }
    }

    /// <summary>
    /// Clears a draft from cache after it has been saved or discarded.
    /// </summary>
    public Task ClearDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        var draftFile = Path.Combine(_draftsPath, $"{draftId}.json");

        try
        {
            if (File.Exists(draftFile))
            {
                File.Delete(draftFile);
                _logger.LogInformation("Cleared draft {DraftId} from cache", draftId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error clearing draft {DraftId} from cache", draftId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the LLM prompt for generating a DecisionSpec draft.
    /// </summary>
    private string BuildDraftPrompt(string instruction)
    {
        return $@"Create a valid DecisionSpec JSON document based on this instruction:
{instruction}

Generate a complete DecisionSpec with:
1. A unique spec_id (use lowercase with underscores)
2. Version (start with V1.0.0.0)
3. Metadata with name, description, and tags
4. At least 2-5 traits (questions) with:
   - key: unique identifier
   - question_text: clear question for users
   - answer_type: ""select"", ""text"", or ""number""
   - required: true/false
   - options: array of possible answers for select types
5. At least 1-3 outcomes with:
   - outcome_id: unique identifier
   - selection_rules: array of rule expressions
   - display_cards: array with title, subtitle, and body_text

Follow this exact JSON structure with snake_case property names:
{{
  ""spec_id"": ""example_spec"",
  ""version"": ""V1.0.0.0"",
  ""metadata"": {{
    ""name"": ""Example Specification"",
    ""description"": ""Description of what this spec does"",
    ""tags"": [""tag1"", ""tag2""]
  }},
  ""traits"": [
    {{
      ""key"": ""question1"",
      ""question_text"": ""What is your goal?"",
      ""answer_type"": ""select"",
      ""required"": true,
      ""options"": [""Option A"", ""Option B"", ""Option C""]
    }}
  ],
  ""outcomes"": [
    {{
      ""outcome_id"": ""outcome1"",
      ""selection_rules"": [""question1 == 'Option A'""],
      ""display_cards"": [
        {{
          ""title"": ""Outcome Title"",
          ""subtitle"": ""Outcome Subtitle"",
          ""body_text"": [""Detailed information about this outcome.""]
        }}
      ]
    }}
  ]
}}

Return ONLY the JSON document, no additional text or explanation.";
    }

    /// <summary>
    /// Parses the LLM response into a DecisionSpecDocument.
    /// </summary>
    private DecisionSpecDocument ParseLlmResponse(string response, string instruction, string draftId)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            throw new InvalidOperationException("LLM returned empty response");
        }

        try
        {
            // Try to extract JSON from markdown code blocks if present
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var lines = json.Split('\n');
                json = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }

            // Parse JSON with snake_case support
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var draft = JsonSerializer.Deserialize<DecisionSpecDocument>(json, options);

            if (draft == null)
            {
                throw new InvalidOperationException("Failed to deserialize LLM response");
            }

            // Set defaults if missing
            draft.SpecId ??= draftId.ToLowerInvariant();
            draft.Version ??= "V1.0.0.0";
            draft.Metadata ??= new DecisionSpecMetadata();
            draft.Metadata.Name ??= "LLM Generated Draft";
            draft.Metadata.Description ??= $"Generated from: {instruction.Substring(0, Math.Min(100, instruction.Length))}...";
            draft.Traits ??= new List<TraitDefinition>();
            draft.Outcomes ??= new List<OutcomeDefinition>();

            return draft;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON");
            throw new InvalidOperationException("LLM response is not valid JSON", ex);
        }
    }

    /// <summary>
    /// Caches a draft to the file system.
    /// </summary>
    private async Task CacheDraftAsync(string draftId, DecisionSpecDocument draft, CancellationToken cancellationToken)
    {
        var draftFile = Path.Combine(_draftsPath, $"{draftId}.json");

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var json = JsonSerializer.Serialize(draft, options);
            await File.WriteAllTextAsync(draftFile, json, cancellationToken);

            _logger.LogInformation("Cached draft {DraftId} to {FilePath}", draftId, draftFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching draft {DraftId}", draftId);
            throw;
        }
    }
}
