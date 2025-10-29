# Swagger UI Guide for DecisionSpark API

## Accessing Swagger UI

**First, make sure the application is running!**

### Start the Application

```bash
cd C:\GitHub\markhazleton\DecisionSpark\DecisionSpark
dotnet run
```

Wait for the message: **"Now listening on: https://localhost:5001"**

### Open Swagger UI

Once the application is running, Swagger UI is available at:

- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`

> **Note**: Keep the terminal/command window open while using the API. Closing it will stop the server.

## Features

### ?? API Key Authentication

Swagger is configured with API Key authentication support.

**To use the API in Swagger:**

1. Click the **"Authorize"** button (lock icon) at the top right
2. Enter the API key: `dev-api-key-change-in-production`
3. Click **"Authorize"**
4. Click **"Close"**

All subsequent requests will include the `X-API-KEY` header automatically.

### ?? Available Endpoints

#### 1. POST /start
**Initialize a decision routing session**

- Expands to show details
- Click **"Try it out"**
- Request body is pre-populated with `{}`
- Click **"Execute"**
- View response with session ID in `nextUrl`

**Sample Response:**
```json
{
  "isComplete": false,
  "texts": ["Thanks! One quick question."],
  "question": {
    "id": "group_size",
    "source": "FAMILY_SATURDAY_V1",
    "text": "How many people are going on the outing?",
    ...
  },
  "nextUrl": "https://api.example.com/v2/pub/conversation/abc123/next"
}
```

**Copy the sessionId** from the `nextUrl` (e.g., `abc123`)

#### 2. POST /v2/pub/conversation/{sessionId}/next
**Continue the conversation with user's answer**

- Click **"Try it out"**
- Paste the **sessionId** from step 1 into the `sessionId` field
- Update request body with user input:

**Example 1: Answer group size**
```json
{
  "user_input": "6 people"
}
```

**Example 2: Answer ages**
```json
{
  "user_input": "ages are 8, 10, 12, 35, 37, 40"
}
```

- Click **"Execute"**
- View next question or final outcome

## ?? Complete Flow in Swagger

### Scenario: Bowling Outcome

1. **Authorize** with API key
2. **POST /start** ? Get first question (group_size)
3. Copy sessionId from response
4. **POST /next** with sessionId and `{"user_input": "6"}` ? Get ages question
5. **POST /next** with sessionId and `{"user_input": "8, 10, 12, 35, 37, 40"}` ? Get Bowling outcome

### Scenario: Movie Night (Immediate)

1. **POST /start** ? Get first question
2. **POST /next** with `{"user_input": "4"}` ? Get ages question
3. **POST /next** with `{"user_input": "3, 6, 35, 37"}` ? Get Movie Night outcome (min_age < 5 triggers immediate rule)

## ?? Response Status Codes

Swagger shows all possible response codes for each endpoint:

- **200 OK** - Success
- **400 Bad Request** - Invalid input or validation error
- **401 Unauthorized** - Missing or invalid API key
- **404 Not Found** - Session not found
- **413 Payload Too Large** - Input exceeds 2KB
- **500 Internal Server Error** - Unexpected error

## ?? Swagger Features

### Schemas
Scroll down to see all data models:
- `StartRequest`
- `StartResponse`
- `NextRequest`
- `NextResponse`
- `QuestionDto`
- `DisplayCardDto`
- `FinalResultDto`
- `ErrorDto`

### Try Different Inputs

Test error handling:
```json
{
  "user_input": "not a number"
}
```

Expected: 400 with error message and retry question

### Request Duration
Swagger UI displays request duration at the bottom of each response.

## ?? Tips

1. **Keep sessionId handy** - Copy it after starting a session
2. **Use the Models section** - Click on schema names to see full structure
3. **Check response examples** - Each endpoint shows expected response shapes
4. **API Key is persistent** - Once authorized, it applies to all requests in the session
5. **Clear and fresh** - Refresh the page to start with a clean state

## ?? Quick Test Script

Open Swagger UI and follow these steps:

1. Click **Authorize**, enter: `dev-api-key-change-in-production`, click Authorize
2. Expand **POST /start**, click **Try it out**, click **Execute**
3. Copy sessionId from `nextUrl` (e.g., if nextUrl is `.../abc123/next`, copy `abc123`)
4. Expand **POST /v2/pub/.../next**, click **Try it out**
5. Paste sessionId in the **sessionId** parameter field
6. In request body, enter: `{"user_input": "6"}`
7. Click **Execute**
8. Repeat steps 4-7 with: `{"user_input": "8, 10, 12, 35, 37, 40"}`
9. See final Bowling outcome!

## ?? Swagger UI Customization

The Swagger UI is configured with:
- Root path (`/`) - No need to navigate to `/swagger`
- Request duration display enabled
- API Key authentication built-in
- XML documentation comments from code
- Full request/response examples

## ?? Notes

- **Development Only**: Swagger UI is only available in Development environment
- **Session Storage**: Sessions are in-memory; restart clears all sessions
- **Canonical URLs**: Spec uses `https://api.example.com` in `next_url`; update in production
- **Logs**: Check `logs/decisionspark-{Date}.txt` for detailed execution logs

## ?? Troubleshooting

### Cannot connect to localhost:5001

**Solution**: Make sure the application is running!

```bash
cd C:\GitHub\markhazleton\DecisionSpark\DecisionSpark
dotnet run
```

Look for: "Now listening on: https://localhost:5001"

### Certificate/SSL errors

**Solution**: Trust the development certificate:

```bash
dotnet dev-certs https --trust
```

Or use HTTP: `http://localhost:5000`

### Port already in use

**Solution**: Stop other instances or change ports in `Properties/launchSettings.json`

For more troubleshooting, see **RUNNING.md**.

---

**Enjoy exploring the DecisionSpark API with Swagger UI!** ??
