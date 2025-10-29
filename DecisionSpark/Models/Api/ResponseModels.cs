namespace DecisionSpark.Models.Api;

public class StartResponse
{
    public bool IsComplete { get; set; }
    public List<string> Texts { get; set; } = new();
    public QuestionDto? Question { get; set; }
    public List<DisplayCardDto> DisplayCards { get; set; } = new();
    public string? CareTypeMessage { get; set; }
    public FinalResultDto? FinalResult { get; set; }
    public string? RawResponse { get; set; }
    public string? NextUrl { get; set; }
    public ErrorDto? Error { get; set; }
}

public class NextResponse
{
    public bool IsComplete { get; set; }
    public List<string> Texts { get; set; } = new();
    public QuestionDto? Question { get; set; }
    public List<DisplayCardDto> DisplayCards { get; set; } = new();
    public string? CareTypeMessage { get; set; }
    public FinalResultDto? FinalResult { get; set; }
    public string? RawResponse { get; set; }
    public string? NextUrl { get; set; }
    public string? PrevUrl { get; set; }
    public ErrorDto? Error { get; set; }
}

public class QuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
 public bool AllowFreeText { get; set; }
    public bool IsFreeText { get; set; }
    public bool AllowMultiSelect { get; set; }
    public bool IsMultiSelect { get; set; }
    public string Type { get; set; } = string.Empty;
    public int? RetryAttempt { get; set; }
}

public class DisplayCardDto
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CareTypeMessage { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public List<string> BodyText { get; set; } = new();
  public List<string> CareTypeDetails { get; set; } = new();
    public List<string> Rules { get; set; } = new();
}

public class FinalResultDto
{
    public string OutcomeId { get; set; } = string.Empty;
    public string ResolutionButtonLabel { get; set; } = string.Empty;
    public string ResolutionButtonUrl { get; set; } = string.Empty;
    public string AnalyticsResolutionCode { get; set; } = string.Empty;
}

public class ErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
