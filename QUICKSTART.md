# Quick Start Guide

## Run the API

```bash
cd DecisionSpark
dotnet run
```

The API will start at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

## Test Endpoint

Use the following requests to test the decision flow.

### 1. Start a Session

**Request:**
```bash
curl -X POST https://localhost:5001/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d "{}" \
  -k
```

**Expected Response:**
```json
{
  "isComplete": false,
  "texts": ["Thanks! One quick question."],
  "question": {
    "id": "group_size",
    "source": "FAMILY_SATURDAY_V1",
    "text": "How many people are going on the outing?",
    "allowFreeText": true,
    "isFreeText": true,
    "allowMultiSelect": false,
    "isMultiSelect": false,
    "type": "text"
  },
  "nextUrl": "https://api.example.com/v2/pub/conversation/abc123/next"
}
```

**Note the sessionId** from the `nextUrl` (e.g., `abc123`).

### 2. Answer Group Size

Replace `{sessionId}` with the actual session ID from step 1.

**Request:**
```bash
curl -X POST https://localhost:5001/v2/pub/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "6 people"}' \
  -k
```

**Expected Response:**
```json
{
  "isComplete": false,
  "texts": ["Thanks! One quick question."],
  "question": {
    "id": "all_ages",
    "source": "FAMILY_SATURDAY_V1",
    "text": "What are the ages of everyone who's going? Please list ages like: 4, 9, 38.",
    ...
  },
  "nextUrl": "https://api.example.com/v2/pub/conversation/abc123/next",
  "prevUrl": "https://api.example.com/v2/pub/conversation/abc123/prev"
}
```

### 3. Answer Ages

**Request:**
```bash
curl -X POST https://localhost:5001/v2/pub/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "ages are 8, 10, 12, 35, 37, 40"}' \
  -k
```

**Expected Response (Bowling Outcome):**
```json
{
  "isComplete": true,
  "texts": ["Here's what I recommend:"],
  "displayCards": [
    {
      "title": "Bowling night ??",
      "subtitle": "Fun for everyone",
      "groupId": "activity_recommendation",
    "careTypeMessage": "Bowling is a great fit for your group.",
  "iconUrl": "https://example.local/icons/bowling.png",
      "bodyText": [
    "It's easy for mixed ages.",
"You can handle a bigger group."
      ],
      "careTypeDetails": [
        "Look for family lanes or bumpers if you need them.",
        "Snack bar = instant dinner."
      ],
      "rules": [
        "Call ahead to reserve a lane if it's Saturday night."
      ]
    }
  ],
  "careTypeMessage": "Bowling is perfect for your crew — easy and fun for everyone.",
  "finalResult": {
    "outcomeId": "GO_BOWLING",
    "resolutionButtonLabel": "Reserve a lane",
    "resolutionButtonUrl": "https://example.local/bowling",
    "analyticsResolutionCode": "ROUTE_BOWLING"
  },
  "rawResponse": "ROUTE_BOWLING"
}
```

## Test Scenarios

### Scenario A: Bowling (4+ people, min_age >= 5)
- Group size: `6`
- Ages: `8, 10, 12, 35, 37, 40`
- **Expected**: `GO_BOWLING`

### Scenario B: Movie Night (immediate: min_age < 5)
- Group size: `4`
- Ages: `3, 6, 35, 37`
- **Expected**: `MOVIE_NIGHT` (triggered by immediate rule)

### Scenario C: Golfing (?4 people, min_age >= 12)
- Group size: `3`
- Ages: `14, 16, 42`
- **Expected**: `GO_GOLFING`

### Scenario D: Invalid Input
- Group size: `not a number`
- **Expected**: 400 error with rephrased question

```bash
curl -X POST https://localhost:5001/v2/pub/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "I have no idea"}' \
  -k
```

**Expected Error Response:**
```json
{
  "error": {
    "code": "INVALID_INPUT",
    "message": "Could not find a number in your response."
  },
  "question": {
    "id": "group_size",
    "text": "Let me try again. How many people are going on the outing?",
    "retryAttempt": 1,
    ...
  },
  "nextUrl": "..."
}
```

## Using Postman or Thunder Client

### Collection Setup

1. **Base URL**: `https://localhost:5001`
2. **Headers** (set for all requests):
   - `Content-Type`: `application/json`
   - `X-API-KEY`: `dev-api-key-change-in-production`

### Request 1: Start
- **Method**: POST
- **URL**: `{{baseUrl}}/start`
- **Body**: `{}`
- **Save sessionId** from response `nextUrl`

### Request 2: Next (Group Size)
- **Method**: POST
- **URL**: `{{baseUrl}}/v2/pub/conversation/{{sessionId}}/next`
- **Body**: `{"user_input": "6 people"}`

### Request 3: Next (Ages)
- **Method**: POST
- **URL**: `{{baseUrl}}/v2/pub/conversation/{{sessionId}}/next`
- **Body**: `{"user_input": "ages: 8, 10, 12, 35, 37, 40"}`

## Check Logs

Logs are written to:
- Console (during development)
- `DecisionSpark/logs/decisionspark-{Date}.txt`

**Example log entries:**
```
[INF] Starting new session abc123 for spec FAMILY_SATURDAY_V1
[DBG] Next trait to collect: group_size
[DBG] Extracted integer: 6
[INF] Stored trait group_size = 6 for session abc123
[DBG] Derived trait min_age = 8
[INF] Single outcome matched: GO_BOWLING
[INF] Session abc123 next processed, complete=True
```

## Troubleshooting

### "Session not found"
- Verify the sessionId in the URL matches the one from `/start`
- Sessions are in-memory; restart clears them

### "Invalid API key"
- Ensure `X-API-KEY` header is set
- Value must match `appsettings.json` ? `DecisionEngine:ApiKey`

### "No active spec found"
- Check that `Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json` exists
- Verify file name pattern: `{SpecId}.{Version}.active.json`

### Rules not matching as expected
- Check logs for derived traits (min_age, max_age, adult_count)
- Review rule expressions in spec JSON
- Ensure operator syntax is correct (e.g., `>=` not `=>`)

## Next Steps

1. ? Verify all three outcomes work
2. ? Test invalid input handling
3. ?? Add OpenAI integration for question phrasing
4. ?? Implement LLM clarifier for tie resolution
5. ?? Add unit tests
6. ?? Create additional DecisionSpecs for other domains

---

**DecisionSpark is ready to guide your users to the right decisions!** ??
