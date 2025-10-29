using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

[ApiController]
[Route("[controller]")]
public class StartController : ControllerBase
{
    private readonly ILogger<StartController> _logger;
    private readonly ISessionStore _sessionStore;
    private readonly IDecisionSpecLoader _specLoader;
    private readonly IRoutingEvaluator _evaluator;
    private readonly IQuestionGenerator _questionGenerator;
 private readonly IResponseMapper _responseMapper;
    private readonly IConfiguration _configuration;

    public StartController(
  ILogger<StartController> logger,
        ISessionStore sessionStore,
 IDecisionSpecLoader specLoader,
   IRoutingEvaluator evaluator,
        IQuestionGenerator questionGenerator,
        IResponseMapper responseMapper,
        IConfiguration configuration)
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _specLoader = specLoader;
     _evaluator = evaluator;
        _questionGenerator = questionGenerator;
        _responseMapper = responseMapper;
        _configuration = configuration;
    }

    [HttpPost]
    public async Task<ActionResult<StartResponse>> Start([FromBody] StartRequest request)
 {
        try
 {
    // Validate API key
            if (!Request.Headers.TryGetValue("X-API-KEY", out var apiKey) || 
 string.IsNullOrEmpty(apiKey) ||
      apiKey != _configuration["DecisionEngine:ApiKey"])
            {
     _logger.LogWarning("Invalid or missing API key");
 return Unauthorized(new { error = "Invalid API key" });
       }

  // Create session
            var session = new DecisionSession
        {
      SessionId = Guid.NewGuid().ToString("N").Substring(0, 12),
        SpecId = _configuration["DecisionEngine:DefaultSpecId"] ?? "FAMILY_SATURDAY_V1",
          Version = "1.0.0",
   KnownTraits = new Dictionary<string, object>()
            };

            _logger.LogInformation("Starting new session {SessionId} for spec {SpecId}", session.SessionId, session.SpecId);

      // Load spec
    var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);

     // Evaluate
          var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);

     // Generate question if needed
      string? questionText = null;
         if (evaluation.NextTraitDefinition != null)
    {
        questionText = await _questionGenerator.GenerateQuestionAsync(spec, evaluation.NextTraitDefinition);
    session.AwaitingTraitKey = evaluation.NextTraitKey;
            }

         // Save session
    await _sessionStore.SaveAsync(session);

            // Map response
      var response = _responseMapper.MapToStartResponse(evaluation, session, spec, questionText);

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
}
