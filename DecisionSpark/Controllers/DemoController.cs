using DecisionSpark.Models.Api;
using DecisionSpark.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Controllers;

/// <summary>
/// Demo controller for interactive conversation flow
/// </summary>
[ApiController]
[Route("demo")]
[Produces("application/json")]
public class DemoController : ControllerBase
{
    private readonly ILogger<DemoController> _logger;
  private readonly ISessionStore _sessionStore;
    private readonly IDecisionSpecLoader _specLoader;
    private readonly IRoutingEvaluator _evaluator;
    private readonly IQuestionGenerator _questionGenerator;
    private readonly IResponseMapper _responseMapper;
    private readonly ITraitParser _traitParser;
    private readonly IConfiguration _configuration;

    public DemoController(
    ILogger<DemoController> logger,
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
    /// Start a demo conversation session
    /// </summary>
    /// <remarks>
    /// Starts a new decision session and returns the first question.
    /// No API key required for demo purposes.
    /// 
    /// Sample request:
  /// 
    ///     GET /demo/start
    /// 
    /// </remarks>
    /// <returns>Demo session with first question</returns>
    /// <response code="200">Demo session started successfully</response>
    [HttpGet("start")]
    [ProducesResponseType(typeof(DemoSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DemoSessionResponse>> StartDemo()
    {
        try
        {
     var session = new Models.Runtime.DecisionSession
            {
         SessionId = Guid.NewGuid().ToString("N")[..12],
        SpecId = _configuration["DecisionEngine:DefaultSpecId"] ?? "FAMILY_SATURDAY_V1",
           Version = "1.0.0",
      KnownTraits = new Dictionary<string, object>()
      };

            _logger.LogInformation("Starting demo session {SessionId}", session.SessionId);

          var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);
            var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);

 string? questionText = null;
    if (evaluation.NextTraitDefinition != null)
   {
                questionText = await _questionGenerator.GenerateQuestionAsync(spec, evaluation.NextTraitDefinition);
      session.AwaitingTraitKey = evaluation.NextTraitKey;
        }

   await _sessionStore.SaveAsync(session);

            var response = new DemoSessionResponse
         {
        SessionId = session.SessionId,
        Message = "Welcome to DecisionSpark! Let's find the perfect activity for your group.",
    CurrentQuestion = questionText ?? "No question available",
         QuestionNumber = 1,
 TotalQuestionsExpected = 2,
  IsComplete = false,
       Instructions = "Answer the question naturally. Examples: '5 people', '3', 'ages: 4, 9, 35, 40'",
              ContinueUrl = $"/demo/answer/{session.SessionId}"
  };

       return Ok(response);
        }
        catch (Exception ex)
        {
        _logger.LogError(ex, "Error in demo start");
            return StatusCode(500, new { error = "Failed to start demo session", details = ex.Message });
      }
    }

    /// <summary>
    /// Answer the current question in a demo session
    /// </summary>
    /// <remarks>
    /// Provide an answer to the current question and receive either the next question
    /// or the final recommendation.
    /// 
    /// Sample requests:
    /// 
    ///     POST /demo/answer/{sessionId}
    ///     {
    ///    "answer": "5 people"
    ///     }
    ///     
    ///POST /demo/answer/{sessionId}
    ///     {
    ///       "answer": "ages are 4, 9, 12, 35, 40"
    ///     }
    /// 
    /// </remarks>
    /// <param name="sessionId">Session ID from the start response</param>
    /// <param name="request">Your answer to the current question</param>
    /// <returns>Next question or final recommendation</returns>
    /// <response code="200">Answer processed successfully</response>
    /// <response code="400">Invalid answer format</response>
    /// <response code="404">Session not found</response>
    [HttpPost("answer/{sessionId}")]
    [ProducesResponseType(typeof(DemoSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DemoSessionResponse>> AnswerQuestion(
        [FromRoute] string sessionId,
 [FromBody] DemoAnswerRequest request)
    {
        try
        {
            var session = await _sessionStore.GetAsync(sessionId);
            if (session == null)
   {
         return NotFound(new { error = "Session not found. Please start a new demo session." });
   }

            var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);
          var awaitingTraitKey = session.AwaitingTraitKey;

            if (string.IsNullOrEmpty(awaitingTraitKey))
        {
         return BadRequest(new { error = "Session is in an invalid state." });
    }

         var traitDef = spec.Traits.FirstOrDefault(t => t.Key == awaitingTraitKey);
            if (traitDef == null)
            {
                return BadRequest(new { error = "Invalid trait key in session." });
       }

 // Parse the answer
            var parseResult = await _traitParser.ParseAsync(
     request.Answer ?? string.Empty,
                awaitingTraitKey,
   traitDef.AnswerType,
                traitDef.ParseHint);

     // Handle invalid input
            if (!parseResult.IsValid)
    {
        _logger.LogWarning("Invalid answer for trait {TraitKey}: {Reason}", awaitingTraitKey, parseResult.ErrorReason);

    session.RetryAttempt++;
 await _sessionStore.SaveAsync(session);

         var errorQuestionText = await _questionGenerator.GenerateQuestionAsync(spec, traitDef, session.RetryAttempt);

     return BadRequest(new
    {
        error = parseResult.ErrorReason ?? "Invalid answer format",
 hint = traitDef.ParseHint,
            currentQuestion = errorQuestionText,
         retryAttempt = session.RetryAttempt,
           examples = GetExamplesForTraitType(traitDef.AnswerType)
     });
}

    // Store the parsed value
            session.KnownTraits[awaitingTraitKey] = parseResult.ExtractedValue!;
            session.RetryAttempt = 0;

   var answeredCount = session.KnownTraits.Count;
     _logger.LogInformation("Stored trait {TraitKey} = {Value} for demo session {SessionId}",
       awaitingTraitKey, parseResult.ExtractedValue, sessionId);

            // Re-evaluate
            var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);

          // Check if complete
       if (evaluation.IsComplete && evaluation.Outcome != null)
    {
     session.IsComplete = true;
          session.AwaitingTraitKey = null;
      await _sessionStore.SaveAsync(session);

                var outcome = evaluation.Outcome;
          return Ok(new DemoSessionResponse
            {
          SessionId = session.SessionId,
       Message = "Great! Here's my recommendation:",
     IsComplete = true,
        QuestionNumber = answeredCount,
     TotalQuestionsExpected = answeredCount,
       Recommendation = new DemoRecommendation
        {
    OutcomeId = outcome.OutcomeId,
     Title = outcome.DisplayCards.FirstOrDefault()?.Title ?? "Recommendation",
         Description = outcome.CareTypeMessage,
        Details = outcome.DisplayCards.FirstOrDefault()?.BodyText ?? new List<string>(),
              ActionLabel = outcome.FinalResult.ResolutionButtonLabel,
      ActionUrl = outcome.FinalResult.ResolutionButtonUrl
           },
   Summary = BuildConversationSummary(session, spec)
     });
         }

          // Get next question
      string? nextQuestionText = null;
            if (evaluation.NextTraitDefinition != null)
            {
                nextQuestionText = await _questionGenerator.GenerateQuestionAsync(spec, evaluation.NextTraitDefinition);
      session.AwaitingTraitKey = evaluation.NextTraitKey;
            }

            await _sessionStore.SaveAsync(session);

       return Ok(new DemoSessionResponse
            {
 SessionId = session.SessionId,
     Message = "Thanks! Next question:",
          CurrentQuestion = nextQuestionText ?? "Processing...",
     QuestionNumber = answeredCount + 1,
     TotalQuestionsExpected = spec.Traits.Count(t => t.Required && !t.IsPseudoTrait),
    IsComplete = false,
   Instructions = "Answer naturally. Type your response below.",
     ContinueUrl = $"/demo/answer/{session.SessionId}",
      AnsweredSoFar = BuildAnswersSummary(session, spec)
     });
      }
        catch (Exception ex)
     {
        _logger.LogError(ex, "Error processing demo answer for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to process answer", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the current status of a demo session
  /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <returns>Current session status</returns>
    [HttpGet("status/{sessionId}")]
  [ProducesResponseType(typeof(DemoSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DemoSessionResponse>> GetSessionStatus(string sessionId)
    {
        try
     {
 var session = await _sessionStore.GetAsync(sessionId);
            if (session == null)
      {
           return NotFound(new { error = "Session not found" });
       }

     var spec = await _specLoader.LoadActiveSpecAsync(session.SpecId);

       if (session.IsComplete)
         {
             var evaluation = await _evaluator.EvaluateAsync(spec, session.KnownTraits);
       if (evaluation.Outcome != null)
           {
                return Ok(new DemoSessionResponse
        {
      SessionId = session.SessionId,
      Message = "Session complete",
             IsComplete = true,
        Recommendation = new DemoRecommendation
         {
            OutcomeId = evaluation.Outcome.OutcomeId,
             Title = evaluation.Outcome.DisplayCards.FirstOrDefault()?.Title ?? "Recommendation",
   Description = evaluation.Outcome.CareTypeMessage
      },
   Summary = BuildConversationSummary(session, spec)
           });
          }
    }

            return Ok(new DemoSessionResponse
        {
                SessionId = session.SessionId,
      Message = "Session in progress",
           CurrentQuestion = session.AwaitingTraitKey != null
           ? spec.Traits.FirstOrDefault(t => t.Key == session.AwaitingTraitKey)?.QuestionText
         : null,
      IsComplete = false,
    AnsweredSoFar = BuildAnswersSummary(session, spec)
   });
        }
catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting demo status for session {SessionId}", sessionId);
          return StatusCode(500, new { error = "Failed to get session status" });
     }
    }

    /// <summary>
    /// Run a complete demo scenario with predefined answers
    /// </summary>
    /// <remarks>
    /// Runs through a complete conversation with pre-filled answers for testing.
    /// 
    /// Available scenarios:
    /// - bowling: Family of 6, ages 8-40 ? Bowling recommendation
    /// - movie: Family of 4 with toddler ? Movie night recommendation
    /// - golf: Group of 3 teens ? Golfing recommendation
    /// 
    /// Sample request:
    /// 
    ///     GET /demo/scenario/bowling
    /// 
    /// </remarks>
 /// <param name="scenarioName">Scenario name (bowling, movie, or golf)</param>
    /// <returns>Complete conversation flow with final recommendation</returns>
    [HttpGet("scenario/{scenarioName}")]
    [ProducesResponseType(typeof(DemoScenarioResult), StatusCodes.Status200OK)]
  public async Task<ActionResult<DemoScenarioResult>> RunScenario(string scenarioName)
    {
        var scenarios = new Dictionary<string, List<string>>
        {
            ["bowling"] = new List<string> { "6 people", "ages are 8, 10, 12, 35, 37, 40" },
            ["movie"] = new List<string> { "4 people", "ages: 3, 6, 35, 37" },
            ["golf"] = new List<string> { "3", "14, 16, 42" }
        };

        if (!scenarios.ContainsKey(scenarioName.ToLower()))
{
            return BadRequest(new
          {
    error = "Unknown scenario",
      availableScenarios = scenarios.Keys.ToList()
            });
        }

        try
      {
         var conversation = new List<DemoConversationStep>();
        var answers = scenarios[scenarioName.ToLower()];

      // Start session
          var startResult = await StartDemo();
    var startResponse = (startResult.Result as OkObjectResult)?.Value as DemoSessionResponse;

         if (startResponse == null)
            {
    return StatusCode(500, new { error = "Failed to start demo" });
  }

       conversation.Add(new DemoConversationStep
     {
       StepNumber = 1,
       Question = startResponse.CurrentQuestion,
          Answer = answers[0],
                IsComplete = false
     });

     var sessionId = startResponse.SessionId;
   DemoSessionResponse? currentResponse = startResponse;

          // Process each answer
            for (int i = 0; i < answers.Count; i++)
      {
           var answerResult = await AnswerQuestion(sessionId, new DemoAnswerRequest { Answer = answers[i] });
     currentResponse = (answerResult.Result as OkObjectResult)?.Value as DemoSessionResponse;

     if (currentResponse == null)
     {
       break;
      }

      if (i < answers.Count - 1 && !currentResponse.IsComplete)
      {
        conversation.Add(new DemoConversationStep
                    {
               StepNumber = i + 2,
 Question = currentResponse.CurrentQuestion,
            Answer = answers[i + 1],
 IsComplete = false
        });
     }
     else if (currentResponse.IsComplete)
        {
    conversation[^1].IsComplete = true;
         break;
            }
    }

    return Ok(new DemoScenarioResult
          {
         ScenarioName = scenarioName,
         SessionId = sessionId,
     Conversation = conversation,
           FinalRecommendation = currentResponse?.Recommendation,
       Message = $"Scenario '{scenarioName}' completed successfully"
  });
        }
        catch (Exception ex)
        {
  _logger.LogError(ex, "Error running demo scenario {Scenario}", scenarioName);
          return StatusCode(500, new { error = "Failed to run scenario", details = ex.Message });
        }
  }

    private List<string> GetExamplesForTraitType(string answerType)
    {
    return answerType.ToLower() switch
        {
            "integer" => new List<string> { "5", "3 people", "6" },
            "integer_list" => new List<string> { "4, 9, 35, 40", "ages: 12, 14, 38", "8, 10, 12, 35, 37, 40" },
            _ => new List<string> { "Type your answer" }
        };
    }

    private List<string> BuildAnswersSummary(Models.Runtime.DecisionSession session, Models.Spec.DecisionSpec spec)
    {
        var summary = new List<string>();
        foreach (var trait in session.KnownTraits)
        {
            var traitDef = spec.Traits.FirstOrDefault(t => t.Key == trait.Key);
  if (traitDef != null && !traitDef.IsPseudoTrait)
            {
     var valueStr = trait.Value is List<int> list
           ? string.Join(", ", list)
           : trait.Value.ToString();
           summary.Add($"{traitDef.QuestionText.Split('?')[0]}: {valueStr}");
      }
        }
 return summary;
    }

    private List<string> BuildConversationSummary(Models.Runtime.DecisionSession session, Models.Spec.DecisionSpec spec)
    {
        var summary = BuildAnswersSummary(session, spec);
  summary.Insert(0, $"Session: {session.SessionId}");
        summary.Add($"Completed: {session.LastUpdatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
  return summary;
    }
}

// Demo-specific DTOs
public class DemoSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CurrentQuestion { get; set; }
    public int QuestionNumber { get; set; }
    public int TotalQuestionsExpected { get; set; }
    public bool IsComplete { get; set; }
    public string? Instructions { get; set; }
    public string? ContinueUrl { get; set; }
    public List<string>? AnsweredSoFar { get; set; }
    public DemoRecommendation? Recommendation { get; set; }
    public List<string>? Summary { get; set; }
}

public class DemoRecommendation
{
    public string OutcomeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
    public string ActionLabel { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
}

public class DemoAnswerRequest
{
    public string Answer { get; set; } = string.Empty;
}

public class DemoScenarioResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public List<DemoConversationStep> Conversation { get; set; } = new();
    public DemoRecommendation? FinalRecommendation { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DemoConversationStep
{
 public int StepNumber { get; set; }
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public bool IsComplete { get; set; }
}
