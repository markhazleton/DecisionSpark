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
    private readonly IOpenAIService _openAIService;

    public TraitParser(
        ILogger<TraitParser> logger,
        IOpenAIService openAIService)
    {
        _logger = logger;
        _openAIService = openAIService;
    }

    public async Task<TraitParseResult> ParseAsync(string userInput, string traitKey, string answerType, string parseHint)
    {
        _logger.LogDebug("Parsing trait {TraitKey} with answer type {AnswerType}", traitKey, answerType);
        _logger.LogInformation("TraitParser received input: '{UserInput}' (Length: {Length}) for trait {TraitKey}", 
            userInput ?? "NULL", userInput?.Length ?? 0, traitKey);

        try
        {
            return answerType.ToLower() switch
            {
                "integer" => await ParseIntegerAsync(userInput, parseHint),
                "integer_list" => await ParseIntegerListAsync(userInput, parseHint),
                "enum" => await ParseEnumAsync(userInput, parseHint),
                _ => new TraitParseResult
                {
                    IsValid = false,
                    ErrorReason = $"Unsupported answer type: {answerType}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing trait {TraitKey}", traitKey);
            return new TraitParseResult
            {
                IsValid = false,
                ErrorReason = "Unexpected error parsing input"
            };
        }
    }

    private async Task<TraitParseResult> ParseIntegerAsync(string input, string parseHint)
    {
        _logger.LogInformation("ParseInteger called with input: '{Input}'", input ?? "NULL");
        
        // First try simple regex extraction
        var numbers = System.Text.RegularExpressions.Regex.Matches(input, @"\d+")
            .Select(m => int.Parse(m.Value))
            .ToList();

        _logger.LogInformation("ParseInteger found {Count} numbers: {Numbers}", 
            numbers.Count, string.Join(", ", numbers));

        if (numbers.Count > 0)
        {
            // Found number with regex - use it
            var value = numbers.First();
            _logger.LogDebug("Extracted integer via regex: {Value}", value);

            return new TraitParseResult
            {
                IsValid = true,
                ExtractedValue = value
            };
        }

        // No number found with regex - try OpenAI if available
        if (_openAIService.IsAvailable())
        {
            _logger.LogDebug("No number found via regex, trying OpenAI parsing");
            var llmResult = await ParseIntegerWithLLMAsync(input, parseHint);
            if (llmResult != null)
            {
                return llmResult;
            }
        }

        // Fallback: couldn't parse
        return new TraitParseResult
        {
            IsValid = false,
            ErrorReason = "Could not find a number in your response."
        };
    }

    private async Task<TraitParseResult?> ParseIntegerWithLLMAsync(string input, string parseHint)
    {
        try
        {
            var systemPrompt = @"You are a number extractor. Your task is to extract a single integer from natural language input.

Rules:
- Extract ONLY the number, nothing else
- Convert written numbers to digits (e.g., 'six' ? 6, 'about 6' ? 6)
- If multiple numbers mentioned, extract the most relevant one
- If no number can be determined, return 'NONE'
- Return ONLY the integer or 'NONE'";

            var userPrompt = $@"Parse hint: {parseHint}
User input: ""{input}""

Extract the integer:";

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 20,
                Temperature = 0.1f // Very low for deterministic parsing
            };

            _logger.LogDebug("Requesting OpenAI to parse integer from natural language");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var parsed = response.Content.Trim();
                
                if (parsed.ToUpper() == "NONE")
                {
                    _logger.LogInformation("LLM could not extract integer from input");
                    return null;
                }

                if (int.TryParse(parsed, out var extractedValue))
                {
                    _logger.LogInformation("LLM parsed integer value: {Value} from input: '{Input}'", 
                        extractedValue, input);
                    return new TraitParseResult
                    {
                        IsValid = true,
                        ExtractedValue = extractedValue
                    };
                }

                _logger.LogWarning("LLM returned non-integer value: {Value}", parsed);
                return null;
            }

            _logger.LogWarning("LLM failed to parse integer: {Error}", response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing integer with LLM");
            return null;
        }
    }

    private async Task<TraitParseResult> ParseIntegerListAsync(string input, string parseHint)
    {
        // First try simple regex extraction
        var numbers = System.Text.RegularExpressions.Regex.Matches(input, @"\d+")
            .Select(m => int.Parse(m.Value))
            .Where(n => n >= 0 && n <= 120)
            .ToList();

        if (numbers.Count > 0)
        {
            _logger.LogDebug("Extracted {Count} ages via regex", numbers.Count);

            return new TraitParseResult
            {
                IsValid = true,
                ExtractedValue = numbers
            };
        }

        // No numbers found with regex - try OpenAI if available
        if (_openAIService.IsAvailable())
        {
            _logger.LogDebug("No numbers found via regex, trying OpenAI parsing for integer list");
            var llmResult = await ParseIntegerListWithLLMAsync(input, parseHint);
            if (llmResult != null)
            {
                return llmResult;
            }
        }

        // Fallback: couldn't parse
        return new TraitParseResult
        {
            IsValid = false,
            ErrorReason = "Could not find valid ages in your response. Please list ages as numbers (0-120)."
        };
    }

    private async Task<TraitParseResult?> ParseIntegerListWithLLMAsync(string input, string parseHint)
    {
        try
        {
            var systemPrompt = @"You are a number list extractor. Your task is to extract multiple integers from natural language input.

Rules:
- Extract ALL numbers mentioned
- Convert written numbers to digits (e.g., 'thirty-five' ? 35)
- Convert ranges to individual numbers (e.g., 'mid-thirties' ? 35)
- Return as comma-separated list: e.g., '8, 10, 35, 40'
- If no numbers can be determined, return 'NONE'
- Each number should be 0-120 (valid ages)";

            var userPrompt = $@"Parse hint: {parseHint}
User input: ""{input}""

Extract the list of integers (comma-separated):";

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 50,
                Temperature = 0.1f // Very low for deterministic parsing
            };

            _logger.LogDebug("Requesting OpenAI to parse integer list from natural language");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var parsed = response.Content.Trim();
                
                if (parsed.ToUpper() == "NONE")
                {
                    _logger.LogInformation("LLM could not extract integers from input");
                    return null;
                }

                // Parse the comma-separated list
                var extractedNumbers = parsed.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => int.TryParse(s, out _))
                    .Select(s => int.Parse(s))
                    .Where(n => n >= 0 && n <= 120)
                    .ToList();

                if (extractedNumbers.Count > 0)
                {
                    _logger.LogInformation("LLM parsed {Count} integers from input: '{Input}' ? [{Values}]", 
                        extractedNumbers.Count, input, string.Join(", ", extractedNumbers));
                    return new TraitParseResult
                    {
                        IsValid = true,
                        ExtractedValue = extractedNumbers
                    };
                }

                _logger.LogWarning("LLM returned invalid integer list: {Value}", parsed);
                return null;
            }

            _logger.LogWarning("LLM failed to parse integer list: {Error}", response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing integer list with LLM");
            return null;
        }
    }

    private async Task<TraitParseResult> ParseEnumAsync(string input, string parseHint)
    {
        // Try simple keyword matching first (fast path)
        var simpleMatch = TrySimpleEnumMatch(input, parseHint);
        if (simpleMatch != null)
        {
            _logger.LogInformation("Simple enum match found: {Value}", simpleMatch);
            return new TraitParseResult
            {
                IsValid = true,
                ExtractedValue = simpleMatch
            };
        }

        // If OpenAI is available, use it for more sophisticated parsing
        if (_openAIService.IsAvailable())
        {
            var llmResult = await ParseEnumWithLLMAsync(input, parseHint);
            if (llmResult != null)
            {
                return llmResult;
            }
        }

        // Fallback: no valid match found
        _logger.LogWarning("Could not parse enum value from input: {Input}", input);
        return new TraitParseResult
        {
            IsValid = false,
            ErrorReason = "Could not understand your response. Please try rephrasing or choose one of the suggested options."
        };
    }

    private string? TrySimpleEnumMatch(string input, string parseHint)
    {
        var normalized = input.ToLower().Trim();

        // Extract expected values from parse hint if present
        // Example: "Map to INDOOR, OUTDOOR, or NO_PREFERENCE"
        if (parseHint.Contains("INDOOR") && (normalized.Contains("indoor") || normalized.Contains("stay in") || normalized.Contains("home")))
            return "INDOOR";
        
        if (parseHint.Contains("OUTDOOR") && (normalized.Contains("outdoor") || normalized.Contains("go out") || normalized.Contains("outside")))
            return "OUTDOOR";
        
        if (parseHint.Contains("NO_PREFERENCE") && (normalized.Contains("no preference") || normalized.Contains("don't care") || normalized.Contains("either")))
            return "NO_PREFERENCE";

        return null;
    }

    private async Task<TraitParseResult> ParseEnumWithLLMAsync(string input, string parseHint)
    {
        try
        {
            var systemPrompt = @"You are a parser that extracts structured values from user input.
Your task: Parse the user's natural language input and map it to one of the expected enum values.

Rules:
- Return ONLY the enum value (uppercase, snake_case)
- If the input doesn't clearly match any option, return 'UNKNOWN'
- Be generous in interpretation but accurate";

            var userPrompt = $@"Parse hint: {parseHint}
User input: ""{input}""

Return the appropriate enum value:";

            var request = new OpenAICompletionRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 50,
                Temperature = 0.3f // Lower temperature for more deterministic parsing
            };

            _logger.LogDebug("Requesting OpenAI to parse enum value");

            var response = await _openAIService.GetCompletionAsync(request);

            if (response.Success && !string.IsNullOrWhiteSpace(response.Content))
            {
                var parsed = response.Content.Trim().ToUpper();
                
                if (parsed == "UNKNOWN")
                {
                    return new TraitParseResult
                    {
                        IsValid = false,
                        ErrorReason = "Could not understand your response. Please try rephrasing."
                    };
                }

                _logger.LogInformation("LLM parsed enum value: {Value}", parsed);
                return new TraitParseResult
                {
                    IsValid = true,
                    ExtractedValue = parsed
                };
            }

            _logger.LogWarning("LLM failed to parse enum: {Error}", response.ErrorMessage);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing enum with LLM");
            return null;
        }
    }
}
