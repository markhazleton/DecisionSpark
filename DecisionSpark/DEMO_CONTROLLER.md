# ?? DecisionSpark Demo Controller

## Overview
The Demo Controller provides a simplified, interactive way to test the DecisionSpark conversation flow without needing API keys or external tooling. Perfect for demonstrations, testing, and understanding how the decision engine works.

## Key Features
? **No API Key Required** - Open access for testing  
? **User-Friendly Responses** - Clear messages and instructions  
? **Pre-Built Scenarios** - Complete flows with one request  
? **Step-by-Step Flow** - Interactive question-answer sessions  
? **Helpful Error Messages** - Examples and hints for invalid inputs  
? **Session Status** - Check progress at any time  

## Quick Start

### 1. Run a Complete Scenario (Fastest!)
```http
GET /demo/scenario/bowling
```

Returns a complete conversation from start to finish:
```json
{
  "scenarioName": "bowling",
  "sessionId": "a1b2c3d4e5f6",
  "conversation": [
    {
      "stepNumber": 1,
      "question": "How many people are going on the outing?",
    "answer": "6 people",
      "isComplete": false
    },
    {
      "stepNumber": 2,
      "question": "What are the ages of everyone who's going?",
    "answer": "ages are 8, 10, 12, 35, 37, 40",
      "isComplete": true
    }
  ],
  "finalRecommendation": {
    "outcomeId": "GO_BOWLING",
 "title": "Bowling night ??",
    "description": "Bowling is perfect for your crew – easy and fun for everyone.",
  "actionLabel": "Reserve a lane",
    "actionUrl": "https://example.local/bowling"
  }
}
```

### 2. Interactive Step-by-Step Flow

#### Step A: Start a Session
```http
GET /demo/start
```

Response:
```json
{
  "sessionId": "a1b2c3d4e5f6",
  "message": "Welcome to DecisionSpark! Let's find the perfect activity for your group.",
  "currentQuestion": "How many people are going on the outing?",
  "questionNumber": 1,
  "totalQuestionsExpected": 2,
  "isComplete": false,
  "instructions": "Answer the question naturally. Examples: '5 people', '3', 'ages: 4, 9, 35, 40'",
  "continueUrl": "/demo/answer/a1b2c3d4e5f6"
}
```

#### Step B: Answer Questions
```http
POST /demo/answer/a1b2c3d4e5f6
Content-Type: application/json

{
  "answer": "6 people"
}
```

Response:
```json
{
  "sessionId": "a1b2c3d4e5f6",
  "message": "Thanks! Next question:",
  "currentQuestion": "What are the ages of everyone who's going? Please list ages like: 4, 9, 38.",
  "questionNumber": 2,
  "totalQuestionsExpected": 2,
  "isComplete": false,
  "answeredSoFar": [
    "How many people are going on the outing: 6"
  ],
  "continueUrl": "/demo/answer/a1b2c3d4e5f6"
}
```

#### Step C: Get Final Recommendation
```http
POST /demo/answer/a1b2c3d4e5f6
Content-Type: application/json

{
  "answer": "8, 10, 12, 35, 37, 40"
}
```

Response:
```json
{
  "sessionId": "a1b2c3d4e5f6",
  "message": "Great! Here's my recommendation:",
  "isComplete": true,
  "recommendation": {
    "outcomeId": "GO_BOWLING",
    "title": "Bowling night ??",
    "description": "Bowling is perfect for your crew – easy and fun for everyone.",
 "details": [
      "It's easy for mixed ages.",
      "You can handle a bigger group."
    ],
    "actionLabel": "Reserve a lane",
    "actionUrl": "https://example.local/bowling"
  },
  "summary": [
    "Session: a1b2c3d4e5f6",
    "How many people are going on the outing: 6",
    "What are the ages of everyone who's going: 8, 10, 12, 35, 37, 40",
    "Completed: 2024-01-15 14:30:00 UTC"
  ]
}
```

## Available Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/demo/start` | GET | Start a new demo session |
| `/demo/answer/{sessionId}` | POST | Answer the current question |
| `/demo/status/{sessionId}` | GET | Check session status |
| `/demo/scenario/{name}` | GET | Run a complete scenario |

## Pre-Built Scenarios

### Bowling Scenario
```http
GET /demo/scenario/bowling
```
- **Group Size:** 6 people
- **Ages:** 8, 10, 12, 35, 37, 40
- **Result:** GO_BOWLING
- **Reason:** Group size >= 4 AND min_age >= 5

### Movie Night Scenario
```http
GET /demo/scenario/movie
```
- **Group Size:** 4 people
- **Ages:** 3, 6, 35, 37
- **Result:** MOVIE_NIGHT
- **Reason:** Immediate rule triggered (min_age < 5)

### Golfing Scenario
```http
GET /demo/scenario/golf
```
- **Group Size:** 3 people
- **Ages:** 14, 16, 42
- **Result:** GO_GOLFING
- **Reason:** Group size <= 4 AND min_age >= 12

## Answer Format Examples

### Group Size Question
Valid answers:
- `"6"` - Just the number
- `"6 people"` - Number with text
- `"six"` - Written out (if supported)
- `"group of 6"` - Natural language

### Ages Question
Valid answers:
- `"8, 10, 12, 35, 37, 40"` - Comma-separated
- `"ages are 8, 10, 12, 35, 37, 40"` - With prefix
- `"ages: 8 10 12 35 37 40"` - Space-separated
- `"8,10,12,35,37,40"` - No spaces

## Error Handling

### Invalid Input Example
```http
POST /demo/answer/a1b2c3d4e5f6
Content-Type: application/json

{
  "answer": "I don't know"
}
```

Response (400 Bad Request):
```json
{
  "error": "Could not find a number in your response.",
  "hint": "Extract a single integer 1-10.",
  "currentQuestion": "Let me try again. How many people are going on the outing?",
  "retryAttempt": 1,
  "examples": [
    "5",
    "3 people",
  "6"
  ]
}
```

## Testing in Swagger UI

1. Open **https://localhost:44356** (or your port)
2. Look for the **"Demo"** section
3. Try endpoints interactively:
   - No API key required!
   - All endpoints are documented
   - Interactive forms for easy testing

## Comparison: Demo vs Production API

| Feature | Demo API | Production API |
|---------|----------|----------------|
| **Base Path** | `/demo/*` | `/start`, `/v2/pub/conversation/*` |
| **API Key** | Not required | Required (X-API-KEY header) |
| **Response Style** | User-friendly, instructional | Standardized, production-ready |
| **Error Messages** | Detailed with examples | Concise, standard codes |
| **Use Case** | Testing, demos, learning | Production applications |
| **Session Management** | Simplified | Full state tracking |
| **Analytics** | Not tracked | Tracked via resolution codes |

## Testing Tips

### 1. Start with Scenarios
Run the pre-built scenarios first to see complete flows:
```http
GET /demo/scenario/bowling
GET /demo/scenario/movie
GET /demo/scenario/golf
```

### 2. Try Interactive Flow
Use start ? answer ? answer pattern for custom testing:
```http
GET /demo/start
# Copy sessionId from response

POST /demo/answer/{sessionId}
# Provide your answer

POST /demo/answer/{sessionId}
# Provide next answer
```

### 3. Test Error Handling
Try invalid inputs to see helpful error messages:
```http
POST /demo/answer/{sessionId}
{
  "answer": "I don't know"
}

POST /demo/answer/{sessionId}
{
  "answer": "abc"
}
```

### 4. Check Session Status
View progress at any time:
```http
GET /demo/status/{sessionId}
```

### 5. Test Edge Cases
- Minimum group size (1 person)
- Maximum group size (10 people)
- All adults (ages 18+)
- Mixed ages (toddlers to adults)
- Boundary conditions

## Use Cases

### ?? Learning
- Understand the decision flow
- See how rules are evaluated
- Learn the conversation pattern

### ?? Testing
- Verify decision logic
- Test edge cases
- Validate recommendations

### ?? Demonstrations
- Show clients how it works
- Live demos without setup
- Quick proof-of-concept

### ?? Debugging
- Trace conversation flow
- Verify rule evaluation
- Check trait parsing

## Sample Test File

A complete test file (`DecisionSpark.Demo.http`) is included with:
- ? All scenario tests
- ? Interactive flow examples
- ? Error handling tests
- ? Edge case coverage
- ? Detailed documentation

Open it in Visual Studio 2022+ or VS Code with REST Client extension.

## Next Steps

1. **Restart the application** to load the demo controller
2. **Try a scenario:** `GET /demo/scenario/bowling`
3. **Test interactively:** `GET /demo/start`
4. **Check Swagger UI** for interactive docs
5. **Use `DecisionSpark.Demo.http`** for comprehensive testing

## Architecture Notes

The Demo Controller:
- ? Reuses all production services (spec loader, evaluator, parsers)
- ? Provides a simplified interface layer
- ? No duplicate logic - wraps existing functionality
- ? Safe for development/staging (no API key = no production use)
- ? Can be excluded from production builds if desired

---

**Happy Testing! ??**

For production API usage, refer to the main API documentation and use the `/start` and `/v2/pub/conversation/*` endpoints with proper API keys.
