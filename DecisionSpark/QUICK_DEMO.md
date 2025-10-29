# ?? Quick Demo - Try It Now!

## Fastest Way to Test DecisionSpark

### Run a Complete Scenario (One Request!)
```http
GET https://localhost:44356/demo/scenario/bowling
```

Returns a complete conversation from start to finish with a recommendation.

### Available Scenarios
- **bowling** - Family of 6 ? Bowling recommendation
- **movie** - Family with toddler ? Movie night recommendation
- **golf** - Teen group ? Golfing recommendation

## Interactive Demo Flow

### 1. Start a Conversation
```http
GET https://localhost:44356/demo/start
```

### 2. Answer Questions
```http
POST https://localhost:44356/demo/answer/{sessionId}
Content-Type: application/json

{
  "answer": "6 people"
}
```

### 3. Get Recommendation
```http
POST https://localhost:44356/demo/answer/{sessionId}
Content-Type: application/json

{
  "answer": "8, 10, 12, 35, 37, 40"
}
```

## Key Features
? **No API Key Required**  
? **User-Friendly Responses**  
? **Helpful Error Messages**  
? **Pre-Built Test Scenarios**  
? **Works in Swagger UI**  

## Test Files Included

### Visual Studio / REST Client
Open `DecisionSpark.Demo.http` and click "Send Request"

### Swagger UI
1. Open https://localhost:44356
2. Look for "Demo" section
3. Try the endpoints interactively

## What's Next?

- ?? **Full documentation:** See `DEMO_CONTROLLER.md`
- ?? **More tests:** Open `DecisionSpark.Demo.http`
- ?? **Production API:** Use `/start` and `/v2/pub/conversation/*` endpoints with API keys

---

**Note:** Demo endpoints are for testing only. For production applications, use the standard API endpoints with proper authentication.
