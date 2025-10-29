using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;
using DecisionSpark.Models.Spec;

namespace DecisionSpark.Services;

public interface IResponseMapper
{
 StartResponse MapToStartResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, string? questionText);
    NextResponse MapToNextResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, string? questionText, int answeredTraitCount);
}

public class ResponseMapper : IResponseMapper
{
    private readonly ILogger<ResponseMapper> _logger;

    public ResponseMapper(ILogger<ResponseMapper> logger)
    {
 _logger = logger;
    }

    public StartResponse MapToStartResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, string? questionText)
    {
 var response = new StartResponse();

      if (evaluation.IsComplete && evaluation.Outcome != null)
        {
     MapCompletionResponse(response, evaluation.Outcome, spec);
        }
   else if (evaluation.NextTraitKey != null && evaluation.NextTraitDefinition != null)
     {
   MapQuestionResponse(response, evaluation.NextTraitDefinition, spec, questionText, session);
   response.NextUrl = $"{spec.CanonicalBaseUrl}/v2/pub/conversation/{session.SessionId}/next";
}
        else if (evaluation.RequiresClarifier)
        {
       // Tie detected - will be handled by clarifier flow
    response.Texts.Add("I need one more detail to make the best recommendation.");
        }

   return response;
    }

    public NextResponse MapToNextResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, string? questionText, int answeredTraitCount)
    {
  var response = new NextResponse();

      if (evaluation.IsComplete && evaluation.Outcome != null)
        {
   MapCompletionResponse(response, evaluation.Outcome, spec);
        }
   else if (evaluation.NextTraitKey != null && evaluation.NextTraitDefinition != null)
   {
     MapQuestionResponse(response, evaluation.NextTraitDefinition, spec, questionText, session);
    response.NextUrl = $"{spec.CanonicalBaseUrl}/v2/pub/conversation/{session.SessionId}/next";
        
       if (answeredTraitCount > 0)
            {
         response.PrevUrl = $"{spec.CanonicalBaseUrl}/v2/pub/conversation/{session.SessionId}/prev";
   }
        }
 else if (evaluation.RequiresClarifier)
        {
  response.Texts.Add("I need one more detail to make the best recommendation.");
   }

 return response;
    }

    private void MapCompletionResponse(dynamic response, OutcomeDefinition outcome, DecisionSpec spec)
    {
        response.IsComplete = true;
      response.Texts = new List<string> { "Here's what I recommend:" };
        response.DisplayCards = outcome.DisplayCards.Select(MapDisplayCard).ToList();
    response.CareTypeMessage = outcome.CareTypeMessage;
        response.FinalResult = MapFinalResult(outcome);
response.RawResponse = outcome.FinalResult.AnalyticsResolutionCode;

  _logger.LogInformation("Mapped completion response for outcome {OutcomeId}", outcome.OutcomeId);
    }

    private void MapQuestionResponse(dynamic response, TraitDefinition trait, DecisionSpec spec, string? questionText, DecisionSession session)
    {
        response.IsComplete = false;
   response.Texts = session.RetryAttempt > 0 
  ? new List<string> { "Let me rephrase that." }
            : new List<string> { "Thanks! One quick question." };

   response.Question = new QuestionDto
     {
     Id = trait.Key,
   Source = spec.SpecId,
       Text = questionText ?? trait.QuestionText,
     AllowFreeText = trait.AnswerType != "enum",
            IsFreeText = trait.AnswerType != "enum",
    AllowMultiSelect = false,
       IsMultiSelect = false,
   Type = "text",
      RetryAttempt = session.RetryAttempt > 0 ? session.RetryAttempt : null
   };

  _logger.LogDebug("Mapped question response for trait {TraitKey}", trait.Key);
    }

    private DisplayCardDto MapDisplayCard(DisplayCard card)
    {
        return new DisplayCardDto
        {
       Title = card.Title,
      Subtitle = card.Subtitle,
            GroupId = card.GroupId,
     CareTypeMessage = card.CareTypeMessage,
  IconUrl = card.IconUrl,
BodyText = card.BodyText,
    CareTypeDetails = card.CareTypeDetails,
  Rules = card.Rules
        };
    }

    private FinalResultDto MapFinalResult(OutcomeDefinition outcome)
    {
     return new FinalResultDto
        {
       OutcomeId = outcome.OutcomeId,
          ResolutionButtonLabel = outcome.FinalResult.ResolutionButtonLabel,
        ResolutionButtonUrl = outcome.FinalResult.ResolutionButtonUrl,
 AnalyticsResolutionCode = outcome.FinalResult.AnalyticsResolutionCode
        };
    }
}
