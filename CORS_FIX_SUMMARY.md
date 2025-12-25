# CORS Error Fix - Summary

## Problem
When using the web UI at https://localhost:44356/, the conversation would start successfully but fail when submitting an answer with a CORS error:

```
Cross-Origin Request Blocked: The Same Origin Policy disallows reading the remote resource at https://api.example.com/v2/pub/conversation/{sessionId}/next
```

## Root Cause
The `ResponseMapper` service was using the `canonical_base_url` from the decision spec JSON file, which was set to a placeholder value of `"https://api.example.com"`. This caused the API responses to return URLs pointing to this non-existent external domain instead of the actual localhost server.

## Files Changed

### 1. DecisionSpark\Services\IResponseMapper.cs
**Changes:**
- Added `HttpContext` dependency to get the actual request URL
- Added `SetHttpContext()` method to interface and implementation
- Created `GetBaseUrl()` helper method that prioritizes the actual request URL over the spec's canonical URL
- Simplified URL paths from `/v2/pub/conversation/` to `/conversation/`
- Updated both `MapToStartResponse()` and `MapToNextResponse()` to use the actual request URL

**Before:**
```csharp
response.NextUrl = $"{spec.CanonicalBaseUrl}/v2/pub/conversation/{session.SessionId}/next";
```

**After:**
```csharp
private string GetBaseUrl(DecisionSpec spec)
{
    if (_httpContext != null)
    {
        var request = _httpContext.Request;
        return $"{request.Scheme}://{request.Host}";
 }
    return spec.CanonicalBaseUrl;
}

response.NextUrl = $"{GetBaseUrl(spec)}/conversation/{session.SessionId}/next";
```

### 2. DecisionSpark\Controllers\StartController.cs
**Changes:**
- Added call to `_responseMapper.SetHttpContext(HttpContext)` before mapping responses
- This ensures the mapper knows the actual request URL when building response URLs

### 3. DecisionSpark\Controllers\ConversationController.cs
**Changes:**
- Simplified route from `[Route("v2/pub/conversation")]` to `[Route("conversation")]`
- Added call to `_responseMapper.SetHttpContext(HttpContext)` before mapping responses
- Updated error response to use actual request URL: `$"{Request.Scheme}://{Request.Host}/conversation/{sessionId}/next"`
- Updated XML documentation comments to reflect new simplified routes

## Result
Now when you:
1. Start a conversation on the web UI
2. Submit an answer

The API will return URLs like:
- `https://localhost:44356/conversation/{sessionId}/next`

Instead of:
- `https://api.example.com/v2/pub/conversation/{sessionId}/next`

This eliminates the CORS error and allows the conversation to flow properly through the web UI.

## Testing
To test the fix:
1. Navigate to https://localhost:44356/
2. Click "Start New Conversation"
3. Answer the first question (e.g., "5 people")
4. The conversation should continue without CORS errors
5. Answer the second question to see the final recommendation

## API Endpoints
The simplified API endpoints are now:
- `POST /start` - Start a new session
- `POST /conversation/{sessionId}/next` - Continue conversation with an answer
- `GET /demo/scenario/{scenarioName}` - Run a complete test scenario
