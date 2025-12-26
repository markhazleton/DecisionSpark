using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

/// <summary>
/// Initializes decision routing sessions
/// </summary>
[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class StartController : ControllerBase
{
    private readonly ILogger<StartController> _logger;
    private readonly ISessionStore _sessionStore;
    private readonly IDecisionSpecLoader _specLoader;
    private readonly IRoutingEvaluator _evaluator;
    private readonly IQuestionGenerator _questionGenerator;
    private readonly IResponseMapper _responseMapper;
    private readonly IConfiguration _configuration;
    private readonly IConversationPersistence _conversationPersistence;

    public StartController(
        ILogger<StartController> logger,
        ISessionStore sessionStore,
        IDecisionSpecLoader specLoader,
        IRoutingEvaluator evaluator,
        IQuestionGenerator questionGenerator,
        IResponseMapper responseMapper,
        IConfiguration configuration,
        IConversationPersistence conversationPersistence)
    {
        _logger = logger;
        _sessionStore = sessionStore;
        _specLoader = specLoader;
        _evaluator = evaluator;
        _questionGenerator = questionGenerator;
        _responseMapper = responseMapper;
        _configuration = configuration;
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
    ///     POST /start
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
    [HttpPost]
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
}
