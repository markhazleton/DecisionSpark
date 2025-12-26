using DecisionSpark.Models.Api;
using DecisionSpark.Models.Runtime;
using DecisionSpark.Models.Spec;
using Microsoft.AspNetCore.Http;

namespace DecisionSpark.Services;

public interface IResponseMapper
{
    StartResponse MapToStartResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, QuestionGenerationResult? questionResult);
    NextResponse MapToNextResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, QuestionGenerationResult? questionResult, int answeredTraitCount);
    void SetHttpContext(HttpContext httpContext);
}

public class ResponseMapper : IResponseMapper
{
    private readonly ILogger<ResponseMapper> _logger;
    private readonly IQuestionPresentationDecider _questionPresentationDecider;
    private HttpContext? _httpContext;

    public ResponseMapper(
        ILogger<ResponseMapper> logger,
        IQuestionPresentationDecider questionPresentationDecider)
    {
        _logger = logger;
        _questionPresentationDecider = questionPresentationDecider;
    }

    public void SetHttpContext(HttpContext httpContext)
    {
        _httpContext = httpContext;
    }

    private string GetBaseUrl(DecisionSpec spec)
    {
        // Use the actual request URL if available, otherwise fall back to spec's canonical URL
        if (_httpContext != null)
        {
            var request = _httpContext.Request;
          return $"{request.Scheme}://{request.Host}";
      }
   return spec.CanonicalBaseUrl;
    }

    public StartResponse MapToStartResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, QuestionGenerationResult? questionResult)
    {
 var response = new StartResponse();

  if (evaluation.IsComplete && evaluation.Outcome != null)
        {
     MapCompletionResponse(response, evaluation.Outcome, spec);
        }
   else if (evaluation.NextTraitKey != null && evaluation.NextTraitDefinition != null)
     {
   MapQuestionResponse(response, evaluation.NextTraitDefinition, spec, questionResult, session);
response.NextUrl = $"{GetBaseUrl(spec)}/conversation/{session.SessionId}/next";
}
        else if (evaluation.RequiresClarifier)
        {
       // Tie detected - will be handled by clarifier flow
    response.Texts.Add("I need one more detail to make the best recommendation.");
}

   return response;
    }

    public NextResponse MapToNextResponse(EvaluationResult evaluation, DecisionSession session, DecisionSpec spec, QuestionGenerationResult? questionResult, int answeredTraitCount)
    {
  var response = new NextResponse();

   if (evaluation.IsComplete && evaluation.Outcome != null)
     {
   MapCompletionResponse(response, evaluation.Outcome, spec);
     }
   else if (evaluation.NextTraitKey != null && evaluation.NextTraitDefinition != null)
   {
     MapQuestionResponse(response, evaluation.NextTraitDefinition, spec, questionResult, session);
    response.NextUrl = $"{GetBaseUrl(spec)}/conversation/{session.SessionId}/next";
      
       if (answeredTraitCount > 0)
            {
  response.PrevUrl = $"{GetBaseUrl(spec)}/conversation/{session.SessionId}/prev";
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

    private void MapQuestionResponse(dynamic response, TraitDefinition trait, DecisionSpec spec, QuestionGenerationResult? questionResult, DecisionSession session)
    {
        response.IsComplete = false;
        response.Texts = session.RetryAttempt > 0 
            ? new List<string> { "Let me rephrase that." }
            : new List<string> { "Thanks! One quick question." };

        // Use QuestionPresentationDecider to determine the question type
        var questionType = _questionPresentationDecider.DecideQuestionType(trait, session);

        // Build metadata with validation hints from session
        var metadata = questionResult?.Metadata ?? new QuestionMetadataDto();
        if (session.ValidationHistory.Any(v => v.TraitKey == trait.Key))
        {
            metadata.ValidationHints = session.ValidationHistory
                .Where(v => v.TraitKey == trait.Key)
                .Select(v => v.ErrorReason)
                .ToList();
        }

        response.Question = new QuestionDto
        {
            Id = trait.Key,
            Source = spec.SpecId,
            Text = questionResult?.QuestionText ?? trait.QuestionText,
            AllowFreeText = metadata.AllowFreeText ?? true, // Default to true per FR-006
            IsFreeText = questionType == "text",
            AllowMultiSelect = questionType == "multi-select",
            IsMultiSelect = questionType == "multi-select",
            Type = questionType,
            RetryAttempt = session.RetryAttempt > 0 ? session.RetryAttempt : null,
            Options = questionResult?.Options ?? new List<QuestionOptionDto>(),
            Metadata = metadata
        };

        _logger.LogDebug("Mapped question response for trait {TraitKey} with type {QuestionType}, allowFreeText={AllowFreeText}", 
            trait.Key, questionType, metadata.AllowFreeText);
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
