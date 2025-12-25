# OpenAI Integration - Error Handling & Fallback Guide

## Overview
The DecisionSpark OpenAI integration includes comprehensive error handling and fallback mechanisms to ensure the system remains operational even when OpenAI services are unavailable.

## Configuration

### Enable/Disable OpenAI
```json
{
  "OpenAI": {
    "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "EnableFallback": true,  // Set to false to fail hard when OpenAI is unavailable
    "TimeoutSeconds": 30
  }
}
```

## Fallback Mechanisms

### 1. Question Generation
**Primary**: OpenAI-generated contextual questions via `OpenAIQuestionGenerator`
**Fallback**: Static question text from JSON spec

**Trigger Conditions**:
- OpenAI not configured (placeholder values in settings)
- Network timeout (>30 seconds by default)
- API authentication failure
- Rate limiting errors
- Service unavailable (503)

**Behavior**:
- On first retry attempt: Uses `GetFallbackQuestion()` which adds format hints
- Example: "Let me try again. How many people are going? (Please provide a single number between 1 and 10)"

### 2. Trait Parsing (Enum Types)
**Primary**: OpenAI LLM parsing for natural language understanding
**Fallback**: Simple keyword matching, then raw input normalization

**Parsing Chain**:
1. Try `TrySimpleEnumMatch()` - Fast keyword-based matching
2. If OpenAI available: `ParseEnumWithLLMAsync()` - Sophisticated NLU
3. Final fallback: Normalize input to uppercase snake_case

**Example**:
- Input: "I'd rather stay home tonight"
- Simple match: `INDOOR` (fast path)
- LLM parsing: Would map "stay home" ? `INDOOR`
- Final fallback: `I_D_RATHER_STAY_HOME_TONIGHT` (rarely used)

### 3. Tie Resolution
**Primary**: OpenAI-generated clarifying questions
**Fallback Chain**:
1. Predefined pseudo-traits from spec (e.g., `preference_activity_style`)
2. LLM-generated dynamic question
3. Return first matched outcome

**Resolution Modes**:
- `PSEUDO_TRAIT_CLARIFIER`: Using predefined pseudo-trait
- `LLM_CLARIFIER`: Using dynamically generated question
- `TIE_FALLBACK`: Returns first outcome when LLM unavailable

## Error Recovery Patterns

### Timeout Handling
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var response = await chatClient.CompleteChatAsync(messages, options, cts.Token);
```

**Recovery**: Automatic fallback to non-LLM methods

### Rate Limiting
- OpenAI SDK handles retry logic automatically
- If persistent: Falls back to static methods
- Logs warning for monitoring

### Configuration Validation
The `OpenAIService` validates configuration on startup:
```csharp
private bool IsConfigurationValid()
{
    return !string.IsNullOrWhiteSpace(_config.Endpoint) &&
           !string.IsNullOrWhiteSpace(_config.ApiKey) &&
           !string.IsNullOrWhiteSpace(_config.DeploymentName) &&
           !_config.ApiKey.Contains("your-") && // Placeholder check
           !_config.Endpoint.Contains("YOUR-");
}
```

## Monitoring & Logging

### Log Levels

**Information Level**:
- `"Azure OpenAI client initialized successfully"`
- `"Generated question for {TraitKey}: {Question}"`
- `"LLM parsed enum value: {Value}"`

**Warning Level**:
- `"OpenAI configuration is incomplete. Service will use fallback mode."`
- `"OpenAI failed, using fallback question for {TraitKey}"`
- `"Could not resolve tie with LLM, using first outcome"`

**Error Level**:
- `"OpenAI request timed out after {Timeout} seconds"`
- `"Error calling OpenAI"`
- `"Error generating question with OpenAI for trait {TraitKey}"`

### Key Metrics to Monitor
1. **OpenAI Availability Rate**: `openAIService.IsAvailable()` checks
2. **Fallback Usage Rate**: Count of `UsedFallback = true` responses
3. **Response Times**: Track completion durations
4. **Error Rates**: Count of failed OpenAI calls by error type

## Testing Without OpenAI

### Development Without Azure OpenAI
1. Leave placeholder values in `appsettings.Development.json`:
   ```json
   {
     "OpenAI": {
       "Endpoint": "https://YOUR-RESOURCE-NAME.openai.azure.com/",
       "ApiKey": "your-development-azure-openai-key"
     }
   }
   ```

2. System will automatically:
   - Detect invalid configuration
   - Log warning message
   - Use fallback implementations
   - Continue normal operation

### Force Fallback Mode
Set `EnableFallback = true` and use placeholder values.

### Test with Real OpenAI
1. Create Azure OpenAI resource
2. Deploy a model (e.g., gpt-4, gpt-35-turbo)
3. Update configuration:
   ```json
   {
     "OpenAI": {
       "Endpoint": "https://YOUR-ACTUAL-RESOURCE.openai.azure.com/",
       "ApiKey": "actual-key-from-azure-portal",
       "DeploymentName": "gpt-4"
     }
   }
   ```

## Best Practices

### 1. Graceful Degradation
? Always provide a working fallback
? Log when using fallback mode
? Don't throw exceptions for OpenAI failures

### 2. Configuration Management
? Use Azure Key Vault for production API keys
? Use User Secrets for development
? Never commit real API keys to source control

Example User Secrets setup:
```bash
dotnet user-secrets init
dotnet user-secrets set "OpenAI:ApiKey" "your-real-key"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
```

### 3. Performance Optimization
? Use lower temperature (0.3) for parsing tasks
? Use higher temperature (0.7) for creative question generation
? Keep MaxTokens low for faster responses
? Implement caching for frequently generated questions (future enhancement)

### 4. Cost Management
? Monitor token usage via Azure portal
? Set appropriate MaxTokens limits
? Use fallback mode in development
? Consider caching common questions

## Troubleshooting

### OpenAI Not Working
1. Check logs for: `"OpenAI service is not configured - using fallback mode"`
2. Verify configuration values don't contain "YOUR-" or "your-"
3. Test connection: `curl` to your Azure OpenAI endpoint
4. Check API key has not expired
5. Verify deployment name matches deployed model

### Slow Response Times
1. Check `TimeoutSeconds` setting (default 30)
2. Monitor Azure OpenAI service health
3. Consider reducing `MaxTokens`
4. Check network latency to Azure region

### High Error Rates
1. Check Azure OpenAI quota limits
2. Monitor rate limiting (requests per minute)
3. Verify deployment is active
4. Check Azure OpenAI service logs

## Future Enhancements

### Potential Improvements
- [ ] Response caching for common questions
- [ ] Circuit breaker pattern for persistent failures
- [ ] Fallback to different model deployments
- [ ] Streaming responses for better UX
- [ ] A/B testing of LLM vs static questions
- [ ] Custom fine-tuned models for domain-specific parsing

## Summary

The OpenAI integration is designed to be **robust and graceful**:
- ? Works without OpenAI configured (fallback mode)
- ? Handles timeouts and errors transparently
- ? Provides clear logging for monitoring
- ? Never blocks the decision flow
- ? Maintains system functionality in all scenarios

The system prioritizes **reliability over sophistication** - it's better to give users a working system with static questions than to fail when LLM services are unavailable.
