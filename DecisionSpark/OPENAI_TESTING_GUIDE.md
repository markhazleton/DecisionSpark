# OpenAI Integration Testing Guide

## Quick Start Testing

### 1. Test Without OpenAI (Fallback Mode)
The system works perfectly without OpenAI configured. This is the easiest way to verify the integration:

```bash
# Run the application with default configuration
cd DecisionSpark
dotnet run
```

**Expected Behavior**:
- App starts successfully
- Console shows: `"OpenAI service is not configured - using fallback mode"`
- All endpoints work using static question text
- No LLM-generated questions or parsing

**Test Endpoints**:
```bash
# Start conversation
curl -X POST https://localhost:5001/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{}'

# Answer questions (use session ID from response)
curl -X POST https://localhost:5001/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "5"}'
```

### 2. Test With OpenAI (Full Integration)

#### Prerequisites
1. Azure OpenAI resource
2. Deployed model (gpt-4 or gpt-35-turbo)
3. API key and endpoint URL

#### Configuration
Update `appsettings.Development.json`:
```json
{
  "OpenAI": {
    "Endpoint": "https://YOUR-ACTUAL-RESOURCE.openai.azure.com/",
    "ApiKey": "your-actual-api-key-from-azure",
    "DeploymentName": "gpt-4",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "EnableFallback": true,
    "TimeoutSeconds": 30
  }
}
```

**Or use User Secrets** (recommended):
```bash
cd DecisionSpark
dotnet user-secrets init
dotnet user-secrets set "OpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "your-actual-key"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"
```

#### Run with OpenAI
```bash
dotnet run
```

**Expected Console Output**:
```
[Startup] Using OpenAI-powered question generator
[10:30:15 INF] Azure OpenAI client initialized successfully
[10:30:15 INF] OpenAI service is configured and available
```

## Test Scenarios

### Scenario 1: Basic Question Generation
**Goal**: Verify OpenAI generates natural questions

**Steps**:
1. Start a conversation: `POST /start`
2. Observe the question text in response
3. Check logs for: `"Generated question for group_size: [question text]"`

**With OpenAI**: Question will be rephrased naturally
**Without OpenAI**: Question will match spec exactly: "How many people are going on the outing?"

### Scenario 2: Error Retry with Better Phrasing
**Goal**: Test OpenAI improves question on retry

**Steps**:
1. Start conversation
2. Provide invalid input: `{"user_input": "lots of people"}`
3. Observe error response with rephrased question
4. Check logs for retry attempt

**Expected**: OpenAI generates clearer question like:
- "Let me rephrase that. Please tell me the exact number of people (between 1 and 10)"

### Scenario 3: Enum Parsing with Natural Language
**Goal**: Test LLM understands natural language for enum values

**Steps**:
1. Create a tie scenario (trigger pseudo-trait question)
2. Provide natural answer like: "I'd rather stay home tonight"
3. System should parse this as `INDOOR`

**Test Cases**:
```bash
# These should all map to INDOOR
"I'd rather stay in"
"Let's stay home"
"indoor activities sound good"
"we prefer staying inside"

# These should map to OUTDOOR
"Let's go out"
"outdoor activities"
"I want to get out of the house"

# These should map to NO_PREFERENCE
"I don't really care"
"either is fine"
"no preference"
```

### Scenario 4: Tie Resolution with LLM Clarifier
**Goal**: Test dynamic tie resolution

**Setup**: Modify spec to create a tie (e.g., two outcomes with same rules)

**Steps**:
1. Answer questions that match multiple outcomes
2. System should ask a clarifying question
3. Check logs for: `"Generated LLM clarifying question for tie"`

**Expected Behavior**:
- With OpenAI: Dynamic, contextual question
- Without OpenAI: Uses predefined pseudo-trait from spec

### Scenario 5: Timeout Handling
**Goal**: Verify graceful degradation on slow responses

**Configuration**: Set very short timeout
```json
"TimeoutSeconds": 1
```

**Expected**: 
- Request times out
- System falls back to static methods
- Log shows: `"OpenAI request timed out after 1 seconds"`
- Conversation continues normally

### Scenario 6: Invalid Configuration
**Goal**: Test startup with bad configuration

**Test Cases**:
1. **Missing API Key**: Leave placeholder value
2. **Wrong Endpoint**: Use invalid URL
3. **Invalid Deployment**: Non-existent model name

**Expected**:
- App starts successfully (doesn't crash)
- Console shows warning about configuration
- System uses fallback mode
- All endpoints work

## Integration Test Examples

### Test 1: Complete Flow Without OpenAI
```bash
# Start
curl -X POST https://localhost:5001/start \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{}'

# Response includes session ID and first question
# Extract sessionId from response

# Answer: group size
curl -X POST https://localhost:5001/conversation/{sessionId}/next \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{"user_input": "5"}'

# Answer: ages
curl -X POST https://localhost:5001/conversation/{sessionId}/next \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{"user_input": "8, 10, 35, 40, 42"}'

# Should return final recommendation
```

### Test 2: Complete Flow With OpenAI
Same as above, but observe:
- Questions are naturally phrased
- Logs show OpenAI calls
- Better handling of ambiguous input

### Test 3: Invalid Input Handling
```bash
# Provide invalid input for integer question
curl -X POST https://localhost:5001/conversation/{sessionId}/next \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{"user_input": "a bunch of people"}'

# Should get error with rephrased question
# With OpenAI: More helpful rephrasing
# Without OpenAI: Static retry message
```

## Monitoring & Validation

### Check OpenAI Status
```bash
# View startup logs
dotnet run 2>&1 | grep -i openai

# Expected outputs:
# - "Using OpenAI-powered question generator"
# - "Azure OpenAI client initialized successfully"
# - "OpenAI service is configured and available"

# Or if not configured:
# - "Using stub question generator"
# - "OpenAI service is not configured - using fallback mode"
```

### Monitor OpenAI Calls
Check logs in `logs/decisionspark-{date}.txt`:

```
[INF] Requesting OpenAI completion. Deployment: gpt-4, MaxTokens: 150
[INF] OpenAI completion received. Length: 87
[INF] Generated question for group_size: How many people will be joining you for the outing?
```

### Verify Fallback Behavior
```
[WRN] OpenAI not available, using fallback
[WRN] OpenAI failed, using fallback question for group_size
[DBG] OpenAI not available, using base question text
```

## Performance Testing

### Measure Response Times

**Without OpenAI** (baseline):
```bash
time curl -X POST https://localhost:5001/start \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{}'

# Expected: < 100ms
```

**With OpenAI**:
```bash
time curl -X POST https://localhost:5001/start \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{}'

# Expected: 500ms - 2000ms (depends on Azure OpenAI region)
```

### Load Testing
```bash
# Install Apache Bench
sudo apt-get install apache2-utils

# Test 100 requests, 10 concurrent
ab -n 100 -c 10 -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -p start-request.json \
  https://localhost:5001/start
```

## Troubleshooting

### OpenAI Not Working
1. Check configuration values
2. Test connection: `curl https://YOUR-RESOURCE.openai.azure.com/`
3. Verify API key in Azure Portal
4. Check deployment name matches deployed model
5. Review logs for specific error messages

### Slow Responses
1. Check Azure OpenAI region (closer = faster)
2. Reduce MaxTokens for faster responses
3. Monitor Azure OpenAI service health
4. Check network latency

### Unexpected Fallback Usage
1. Verify configuration doesn't have placeholder values
2. Check for network connectivity issues
3. Review error logs for OpenAI failures
4. Verify API quota not exceeded

## Success Criteria

? **Build**: Project compiles without errors  
? **Startup**: Application starts without OpenAI configured  
? **Fallback**: All endpoints work in fallback mode  
? **OpenAI Init**: Client initializes with valid configuration  
? **Question Gen**: OpenAI generates natural questions  
? **Retry Logic**: Better questions on retry attempts  
? **Enum Parsing**: LLM understands natural language enum values  
? **Tie Resolution**: Dynamic clarifying questions work  
? **Error Handling**: Graceful degradation on OpenAI failures  
? **Logging**: Clear logs for monitoring and debugging  

## Next Steps

1. **Run basic tests**: Verify fallback mode works
2. **Configure OpenAI**: Set up Azure OpenAI if available
3. **Test all scenarios**: Follow test scenarios above
4. **Monitor logs**: Check for errors or warnings
5. **Measure performance**: Compare with/without OpenAI
6. **Optimize**: Adjust timeout, MaxTokens based on needs

## Additional Resources

- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [OpenAI Integration Guide](./OPENAI_INTEGRATION_GUIDE.md)
- [API Documentation](https://localhost:5001/swagger)
