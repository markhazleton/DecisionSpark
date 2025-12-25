namespace DecisionSpark.Services;

public class TraitParseResult
{
    public bool IsValid { get; set; }
    public object? ExtractedValue { get; set; }
    public string? ErrorReason { get; set; }
}

public interface ITraitParser
{
    Task<TraitParseResult> ParseAsync(string userInput, string traitKey, string answerType, string parseHint);
}

public class TraitParser : ITraitParser
{
    private readonly ILogger<TraitParser> _logger;

    public TraitParser(ILogger<TraitParser> logger)
    {
        _logger = logger;
}

    public Task<TraitParseResult> ParseAsync(string userInput, string traitKey, string answerType, string parseHint)
    {
        _logger.LogDebug("Parsing trait {TraitKey} with answer type {AnswerType}", traitKey, answerType);
        _logger.LogInformation("TraitParser received input: '{UserInput}' (Length: {Length}) for trait {TraitKey}", 
         userInput ?? "NULL", userInput?.Length ?? 0, traitKey);

try
        {
 return answerType.ToLower() switch
       {
       "integer" => ParseInteger(userInput, parseHint),
        "integer_list" => ParseIntegerList(userInput, parseHint),
            "enum" => ParseEnum(userInput, parseHint),
    _ => Task.FromResult(new TraitParseResult
       {
        IsValid = false,
 ErrorReason = $"Unsupported answer type: {answerType}"
     })
 };
        }
 catch (Exception ex)
        {
          _logger.LogError(ex, "Error parsing trait {TraitKey}", traitKey);
            return Task.FromResult(new TraitParseResult
   {
  IsValid = false,
      ErrorReason = "Unexpected error parsing input"
  });
        }
    }

    private Task<TraitParseResult> ParseInteger(string input, string parseHint)
  {
        _logger.LogInformation("ParseInteger called with input: '{Input}'", input ?? "NULL");
    
  var numbers = System.Text.RegularExpressions.Regex.Matches(input, @"\d+")
  .Select(m => int.Parse(m.Value))
.ToList();

        _logger.LogInformation("ParseInteger found {Count} numbers: {Numbers}", 
            numbers.Count, string.Join(", ", numbers));

  if (numbers.Count == 0)
{
         return Task.FromResult(new TraitParseResult
        {
      IsValid = false,
  ErrorReason = "Could not find a number in your response."
        });
    }

        // Take the first or largest number based on hint
      var value = numbers.First();
        _logger.LogDebug("Extracted integer: {Value}", value);

        return Task.FromResult(new TraitParseResult
      {
   IsValid = true,
       ExtractedValue = value
        });
    }

    private Task<TraitParseResult> ParseIntegerList(string input, string parseHint)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(input, @"\d+")
  .Select(m => int.Parse(m.Value))
            .Where(n => n >= 0 && n <= 120)
            .ToList();

        if (numbers.Count == 0)
  {
       return Task.FromResult(new TraitParseResult
            {
    IsValid = false,
  ErrorReason = "Could not find valid ages in your response. Please list ages as numbers (0-120)."
         });
}

        _logger.LogDebug("Extracted {Count} ages", numbers.Count);

   return Task.FromResult(new TraitParseResult
 {
            IsValid = true,
            ExtractedValue = numbers
        });
    }

    private Task<TraitParseResult> ParseEnum(string input, string parseHint)
    {
        // For now, return the raw input as the enum value
    // LLM mapping service will handle classification
     return Task.FromResult(new TraitParseResult
        {
   IsValid = true,
            ExtractedValue = input
        });
    }
}
