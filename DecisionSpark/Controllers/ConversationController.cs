using DecisionSpark.Common;
using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;
using DecisionSpark.Models.Spec;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

/// <summary>
/// Manages decision routing conversations
/// </summary>
[ApiController]
[Route("conversation")]
[Produces("application/json")]
public class ConversationController : ControllerBase
{
    private readonly ILogger<ConversationController> _logger;
    private readonly ISessionStore _sessionStore;
    private readonly IDecisionSpecLoader _specLoader;
    private readonly IRoutingEvaluator _evaluator;
    private readonly IQuestionGenerator _questionGenerator;
    private readonly IResponseMapper _responseMapper;
    private readonly ITraitParser _traitParser;
    private readonly IConfiguration _configuration;
    private readonly IUserSelectionService _userSelectionService;
    private readonly IQuestionPresentationDecider _questionPresentationDecider;
    private readonly IConversationPersistence _conversationPersistence;

    public ConversationController(
        ILogger<ConversationController> logger,
        ISessionStore sessionStore,
        IDecisionSpecLoader specLoader,
        IRoutingEvaluator evaluator,
        IQuestionGenerator questionGenerator,
        IResponseMapper responseMapper,
        ITraitParser traitParser,
        IConfiguration configuration,
        IUserSelectionService userSelectionService,
        IQuestionPresentationDecider questionPresentationDecider,
        IConversationPersistence conversationPersistence)
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _specLoader = specLoader;
        _evaluator = evaluator;
        _questionGenerator = questionGenerator;
        _responseMapper = responseMapper;
        _traitParser = traitParser;
        _configuration = configuration;
        _userSelectionService = userSelectionService;
        _questionPresentationDecider = questionPresentationDecider;
        _conversationPersistence = conversationPersistence;
    }

    /// <summary>
    /// Get list of available DecisionSpecs
    /// </summary>
    /// <returns>List of available spec IDs and metadata</returns>
    /// <response code="200">Successfully retrieved specs list</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("specs")]
    [ProducesResponseType(typeof(SpecListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult<SpecListResponse> GetSpecs()
    {
        try
        {
            var configPath = _configuration["DecisionEngine:ConfigPath"] ?? "Config/DecisionSpecs";
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), configPath);
            
            _logger.LogInformation("Scanning for specs in: {Path}", fullPath);
            
            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning("Config path does not exist: {Path}", fullPath);
                return Ok(new SpecListResponse { Specs = new List<SpecInfo>() });
            }

            var specFiles = Directory.GetFiles(fullPath, "*.active.json")
                .Select(file => 
                {
                    var fileName = Path.GetFileName(file);
                    // Extract spec ID from filename (e.g., "FAMILY_SATURDAY_V1.0.0.0.active.json" -> "FAMILY_SATURDAY_V1")
                    var specId = fileName.Replace(".active.json", "");
                    var parts = specId.Split('.');
                    var baseId = parts[0];
                    
                    return new SpecInfo
                    {
                        SpecId = baseId,
                        FileName = fileName,
                        DisplayName = baseId.Replace('_', ' '),
                        IsDefault = baseId == (_configuration["DecisionEngine:DefaultSpecId"] ?? "FAMILY_SATURDAY_V1")
                    };
                })
                .OrderBy(s => s.DisplayName)
                .ToList();

            _logger.LogInformation("Found {Count} spec(s)", specFiles.Count);

            return Ok(new SpecListResponse { Specs = specFiles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving specs list");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Start a new decision routing session
    /// </summary>
    /// <remarks>
    /// Creates a new session and returns either the first question to collect traits
    /// or a final recommendation if an outcome can already be determined.
    /// 
    /// Sample request:
/// 
    ///     POST /conversation/start
    ///     {
    ///         "spec_id": "TECH_STACK_ADVISOR_V1"
    ///     }
    /// 
    /// </remarks>
    /// <param name="request">Request with optional spec_id to use (defaults to configured DefaultSpecId)</param>
    /// <returns>First question or final outcome</returns>
    /// <response code="200">Successfully started session</response>
    /// <response code="401">Invalid or missing API key (handled by middleware)</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StartResponse>> Start([FromBody] StartRequest request)
    {
        try
        {
            // Determine which spec to use
            var specId = !string.IsNullOrWhiteSpace(request.SpecId) 
                ? request.SpecId 
                : _configuration["DecisionEngine:DefaultSpecId"] ?? "FAMILY_SATURDAY_V1";

            // Create session
            var session = new DecisionSession
            {
                SessionId = Guid.NewGuid().ToString("N").Substring(0, 12),
                SpecId = specId,
                Version = "1.0.0",
                KnownTraits = new Dictionary<string, object>()
            };

            _logger.LogInformation("Starting new session {SessionId} for spec {SpecId}", session.SessionId, session.SpecId);

            // Load spec
            var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);

            // Evaluate
            var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);

            // Generate question if needed
            QuestionGenerationResult? questionResult = null;
            if (evaluation.NextTraitDefinition != null)
            {
                questionResult = await _questionGenerator.GenerateQuestionWithOptionsAsync(spec, evaluation.NextTraitDefinition);
                session.AwaitingTraitKey = evaluation.NextTraitKey;
            }

            // Save session
            await _sessionStore.SaveAsync(session);

            // Persist conversation to disk
            await _conversationPersistence.SaveConversationAsync(session);

            // Map response with HttpContext
            _responseMapper.SetHttpContext(HttpContext);
            var response = _responseMapper.MapToStartResponse(evaluation, session, spec, questionResult);

            _logger.LogInformation("Session {SessionId} started successfully, complete={IsComplete}",
                session.SessionId, evaluation.IsComplete);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Start endpoint");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Continue a decision routing session with user's answer
    /// </summary>
    /// <remarks>
    /// Accepts the user's answer to the current question, parses and validates it,
    /// then returns either the next question or a final recommendation.
  /// 
    /// Sample request for free-text answer:
/// 
    ///     POST /conversation/{sessionId}/next
    ///     {
    ///       "user_input": "5 people: ages 4, 9, 38, 40, 12"
    ///     }
    /// 
    /// Sample request for option-based answer (future):
  /// 
    ///     POST /conversation/{sessionId}/next
    ///     {
    ///       "selected_option_ids": [101, 203],
    ///"selected_option_texts": ["Fever", "Cough"]
    ///   }
    /// 
    /// </remarks>
    /// <param name="sessionId">Session ID from the previous response's next_url</param>
    /// <param name="request">User's answer</param>
    /// <returns>Next question or final outcome</returns>
    /// <response code="200">Successfully processed answer</response>
    /// <response code="400">Invalid input or session state</response>
    /// <response code="401">Invalid or missing API key (handled by middleware)</response>
    /// <response code="404">Session not found</response>
    /// <response code="413">Input too large (>2KB)</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{sessionId}/next")]
    [ProducesResponseType(typeof(NextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(NextResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NextResponse>> Next(
        [FromRoute] string sessionId,
        [FromBody][ModelBinder(BinderType = typeof(NextRequestBinder))] NextRequest request)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Next endpoint called for session {SessionId}. Request object null: {IsNull}, UserInput: '{UserInput}'",
                sessionId, request == null, request?.UserInput ?? "NULL");

            // Validate input size
            if (request.UserInput?.Length > Constants.MAX_INPUT_SIZE)
            {
                _logger.LogWarning("Input too large for session {SessionId}", sessionId);
                return StatusCode(413, new { error = Constants.ErrorCodes.INPUT_TOO_LARGE, message = "Input too large" });
            }

            // Get session
            var session = await _sessionStore.GetAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Session not found: {SessionId}", sessionId);
                return NotFound(new { error = Constants.ErrorCodes.SESSION_NOT_FOUND, message = "Session not found" });
            }

            _logger.LogInformation("Processing next for session {SessionId}, awaiting trait {TraitKey}",
                sessionId, session.AwaitingTraitKey);

            // Load spec
            var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);

            // Determine which trait we're expecting
            var awaitingTraitKey = session.AwaitingTraitKey;
            if (string.IsNullOrEmpty(awaitingTraitKey))
            {
                _logger.LogError("Session {SessionId} has no awaiting trait", sessionId);
                return BadRequest(new { error = Constants.ErrorCodes.SESSION_STATE_INVALID, message = "Session state invalid" });
            }

            var traitDef = FindTraitDefinition(spec, awaitingTraitKey);
            if (traitDef == null)
            {
                _logger.LogError("Trait {TraitKey} not found in spec (checked both traits and pseudo_traits)", awaitingTraitKey);
                return BadRequest(new { error = Constants.ErrorCodes.SESSION_STATE_INVALID, message = "Invalid trait key" });
            }

            // Get current question type to properly normalize selection
            var currentQuestionType = _questionPresentationDecider.DecideQuestionType(traitDef, session);
            
            // Generate question to get available options for validation
            var currentQuestionResult = await _questionGenerator.GenerateQuestionWithOptionsAsync(spec, traitDef, session.RetryAttempt);
            
            // Normalize user selection (structured options override free text per FR-024a)
            var normalizedSelection = _userSelectionService.NormalizeSelection(
                request,
                currentQuestionType,
                currentQuestionResult.Options);

            _logger.LogDebug("Normalized selection for trait {TraitKey}: type={QuestionType}, values={Values}, text='{Text}'",
                awaitingTraitKey, normalizedSelection.QuestionType, 
                normalizedSelection.SelectedValues != null ? string.Join(",", normalizedSelection.SelectedValues) : "NULL",
                normalizedSelection.SubmittedText ?? "NULL");

            // Determine the input to parse based on normalization results
            string inputToParse;
            if (normalizedSelection.SelectedValues != null && normalizedSelection.SelectedValues.Length > 0)
            {
                // Use structured values if available
                inputToParse = string.Join(", ", normalizedSelection.SelectedValues);
            }
            else
            {
                // Fall back to submitted text
                inputToParse = normalizedSelection.SubmittedText ?? request.UserInput ?? string.Empty;
            }

            // Parse the normalized input
            var parseResult = await _traitParser.ParseAsync(
                inputToParse,
                awaitingTraitKey,
                traitDef.AnswerType,
                traitDef.ParseHint,
                session);

            // Handle invalid input
            if (!parseResult.IsValid)
            {
                _logger.LogWarning("Invalid input for trait {TraitKey}: {Reason}", awaitingTraitKey, parseResult.ErrorReason);

                // Track validation failure
                session.RetryAttempt++;
                session.ValidationHistory.Add(new Models.Runtime.ValidationHistoryEntry
                {
                    TraitKey = awaitingTraitKey,
                    Attempt = session.RetryAttempt,
                    InputTypeUsed = "text", // Will be enhanced when we track actual input type
                    ErrorReason = parseResult.ErrorReason ?? "Invalid input",
                    TimestampUtc = DateTime.UtcNow
                });
                
                var questionType = _questionPresentationDecider.DecideQuestionType(traitDef, session);
                await _sessionStore.SaveAsync(session);

                var errorQuestionResult = await _questionGenerator.GenerateQuestionWithOptionsAsync(spec, traitDef, session.RetryAttempt);

                // Log telemetry for failed attempt
                var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation(
                    "[Telemetry] Session={SessionId}, Trait={TraitKey}, Attempt={Attempt}, Success=false, Latency={Latency}ms, QuestionType={QuestionType}",
                    sessionId, awaitingTraitKey, session.RetryAttempt, latency, questionType);

                var errorResponse = new NextResponse
                {
                    Error = new ErrorDto
                    {
                        Code = Constants.ErrorCodes.INVALID_INPUT,
                        Message = parseResult.ErrorReason ?? "Invalid input"
                    },
                    Question = new QuestionDto
                    {
                        Id = traitDef.Key,
                        Source = spec.SpecId,
                        Text = errorQuestionResult.QuestionText,
                        AllowFreeText = questionType == "text" || questionType == "multi-select",
                        IsFreeText = questionType == "text",
                        AllowMultiSelect = questionType == "multi-select",
                        IsMultiSelect = questionType == "multi-select",
                        Type = questionType,
                        RetryAttempt = session.RetryAttempt,
                        Options = errorQuestionResult.Options,
                        Metadata = errorQuestionResult.Metadata
                    },
                    NextUrl = $"{Request.Scheme}://{Request.Host}/conversation/{sessionId}/next"
                };

                return BadRequest(errorResponse);
            }

            // Store the parsed value
            session.KnownTraits[awaitingTraitKey] = parseResult.ExtractedValue!;
            session.RetryAttempt = 0; // Reset on successful parse
            _logger.LogInformation("Stored trait {TraitKey} = {Value} for session {SessionId}",
                awaitingTraitKey, parseResult.ExtractedValue, sessionId);

            // Re-evaluate
            var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);

            // Generate question if needed
            QuestionGenerationResult? questionResult = null;
            if (evaluation.NextTraitDefinition != null)
            {
                questionResult = await _questionGenerator.GenerateQuestionWithOptionsAsync(spec, evaluation.NextTraitDefinition);
                session.AwaitingTraitKey = evaluation.NextTraitKey;
            }
            else
            {
                session.AwaitingTraitKey = null;
                session.IsComplete = evaluation.IsComplete;
            }

            // Save session
            await _sessionStore.SaveAsync(session);

            // Persist conversation to disk
            await _conversationPersistence.SaveConversationAsync(session);

            // Map response with HttpContext
            _responseMapper.SetHttpContext(HttpContext);
            var answeredCount = session.KnownTraits.Count(kv => spec.Traits.Any(t => t.Key == kv.Key && !t.IsPseudoTrait));
            var response = _responseMapper.MapToNextResponse(evaluation, session, spec, questionResult, answeredCount);

            // Log telemetry for successful processing
            var successLatency = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var isFirstAttempt = session.RetryAttempt == 0;
            _logger.LogInformation(
                "[Telemetry] Session={SessionId}, Trait={TraitKey}, Success=true, FirstAttempt={FirstAttempt}, Latency={Latency}ms, Complete={IsComplete}",
                sessionId, awaitingTraitKey, isFirstAttempt, successLatency, evaluation.IsComplete);

            _logger.LogInformation("Session {SessionId} next processed, complete={IsComplete}",
                sessionId, evaluation.IsComplete);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Next endpoint for session {SessionId}", sessionId);
            return StatusCode(500, new { error = Constants.ErrorCodes.INTERNAL_ERROR, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Finds a trait definition by key, checking both regular traits and pseudo traits
    /// </summary>
    private TraitDefinition? FindTraitDefinition(DecisionSpec spec, string traitKey)
    {
        // First check regular traits
        var trait = spec.Traits.FirstOrDefault(t => t.Key == traitKey);
        if (trait != null)
        {
            return trait;
        }

        // Then check pseudo traits in tie strategy
        return spec.TieStrategy?.PseudoTraits?.FirstOrDefault(t => t.Key == traitKey);
    }
}
