using DecisionSpark.Models.Api;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

[ApiController]
[Route("v2/pub/conversation")]
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

    [HttpPost("{sessionId}/next")]
    public async Task<ActionResult<NextResponse>> Next(string sessionId, [FromBody] NextRequest request)
    {
        try
  {
   // Validate API key
            if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || 
       string.IsNullOrEmpty(apiKey) ||
     apiKey != _configuration["DecisionEngine:ApiKey"])
            {
          _logger.LogWarning("Invalid or missing API key for session {SessionId}", sessionId);
          return Unauthorized(new { error = "Invalid API key" });
        }

            // Validate input size
            if (request.UserInput?.Length > 2048)
  {
         _logger.LogWarning("Input too large for session {SessionId}", sessionId);
   return StatusCode(413, new { error = "Input too large" });
  }

   // Get session
  var session = await _sessionStore.GetAsync(sessionId);
       if (session == null)
  {
                _logger.LogWarning("Session not found: {SessionId}", sessionId);
                return NotFound(new { error = "Session not found" });
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
       return BadRequest(new { error = "Session state invalid" });
            }

 var traitDef = spec.Traits.FirstOrDefault(t => t.Key == awaitingTraitKey);
  if (traitDef == null)
            {
_logger.LogError("Trait {TraitKey} not found in spec", awaitingTraitKey);
           return BadRequest(new { error = "Invalid trait key" });
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
         Code = "INVALID_INPUT",
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
             NextUrl = $"{spec.CanonicalBaseUrl}/v2/pub/conversation/{sessionId}/next"
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

        // Map response
      var answeredCount = session.KnownTraits.Count(kv => spec.Traits.Any(t => t.Key == kv.Key && !t.IsPseudoTrait));
            var response = _responseMapper.MapToNextResponse(evaluation, session, spec, questionText, answeredCount);

            _logger.LogInformation("Session {SessionId} next processed, complete={IsComplete}", 
           sessionId, evaluation.IsComplete);

            return Ok(response);
        }
  catch (Exception ex)
        {
_logger.LogError(ex, "Error in Next endpoint for session {SessionId}", sessionId);
          return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
