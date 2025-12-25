using DecisionSpark.Common;
using DecisionSpark.Models.Api;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

/// <summary>
/// Continues decision routing conversations
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

    public ConversationController(
        ILogger<ConversationController> logger,
        ISessionStore sessionStore,
        IDecisionSpecLoader specLoader,
        IRoutingEvaluator evaluator,
        IQuestionGenerator questionGenerator,
        IResponseMapper responseMapper,
        ITraitParser traitParser,
        IConfiguration configuration)
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _specLoader = specLoader;
        _evaluator = evaluator;
        _questionGenerator = questionGenerator;
        _responseMapper = responseMapper;
        _traitParser = traitParser;
        _configuration = configuration;
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

            var traitDef = spec.Traits.FirstOrDefault(t => t.Key == awaitingTraitKey);
            if (traitDef == null)
            {
                _logger.LogError("Trait {TraitKey} not found in spec", awaitingTraitKey);
                return BadRequest(new { error = Constants.ErrorCodes.SESSION_STATE_INVALID, message = "Invalid trait key" });
            }

            // Parse the input
            var parseResult = await _traitParser.ParseAsync(
                request.UserInput ?? string.Empty,
                awaitingTraitKey,
                traitDef.AnswerType,
                traitDef.ParseHint);

            // Handle invalid input
            if (!parseResult.IsValid)
            {
                _logger.LogWarning("Invalid input for trait {TraitKey}: {Reason}", awaitingTraitKey, parseResult.ErrorReason);

                session.RetryAttempt++;
                await _sessionStore.SaveAsync(session);

                var errorQuestionText = await _questionGenerator.GenerateQuestionAsync(spec, traitDef, session.RetryAttempt);

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
                        Text = errorQuestionText,
                        AllowFreeText = traitDef.AnswerType != "enum",
                        IsFreeText = traitDef.AnswerType != "enum",
                        AllowMultiSelect = false,
                        IsMultiSelect = false,
                        Type = "text",
                        RetryAttempt = session.RetryAttempt
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
            string? questionText = null;
            if (evaluation.NextTraitDefinition != null)
            {
                questionText = await _questionGenerator.GenerateQuestionAsync(spec, evaluation.NextTraitDefinition);
                session.AwaitingTraitKey = evaluation.NextTraitKey;
            }
            else
            {
                session.AwaitingTraitKey = null;
                session.IsComplete = evaluation.IsComplete;
            }

            // Save session
            await _sessionStore.SaveAsync(session);

            // Map response with HttpContext
            _responseMapper.SetHttpContext(HttpContext);
            var answeredCount = session.KnownTraits.Count(kv => spec.Traits.Any(t => t.Key == kv.Key && !t.IsPseudoTrait));
            var response = _responseMapper.MapToNextResponse(evaluation, session, spec, questionText, answeredCount);

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
}
